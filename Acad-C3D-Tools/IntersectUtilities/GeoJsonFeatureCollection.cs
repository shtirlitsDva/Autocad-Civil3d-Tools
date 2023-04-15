using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.DataShortcuts;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Dreambuild.AutoCAD;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeSchedule;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Result = IntersectUtilities.Result;

namespace IntersectUtilities
{
    public class GeoJsonFeatureCollection
    {
        [JsonPropertyName("type")]
        public string Type { get; } = "FeatureCollection";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "FeatureCollection";

        [JsonPropertyName("features")]
        public List<GeoJsonFeature> Features { get; set; } = new List<GeoJsonFeature>();

        [JsonPropertyName("crs")]
        public GeoJsonCRS CRS { get; set; } = new GeoJsonCRS();

        public GeoJsonFeatureCollection(string name)
        {
            Name = name;
        }

        public void AddViewFrameAsLineString(ViewFrame viewFrame)
        {
            var feature = new GeoJsonFeature();
            feature.Properties.Add("DwgNumber", viewFrame.Name);
            feature.Geometry = new GeoJsonLineString(viewFrame);
            Features.Add(feature);
        }

        public void AddFjvPolylineAsLineString(
            Polyline polyline, Dictionary<string, object> props)
        {
            var feature = new GeoJsonFeature();
            feature.Properties = props;
            feature.Geometry = new GeoJsonLineString(polyline);
            Features.Add(feature);
        }

        public void AddFjvBlockAsGeometryCollection(
            List<Entity> ents, Dictionary<string, object> props)
        {
            var feature = new GeoJsonFeature();
            feature.Properties = props;
            var gjgc = new GeoJsonGeometryCollection();
            foreach (var ent in ents)
            {
                switch (ent)
                {
                    case Line line:
                        gjgc.Geometries.Add(
                            new GeoJsonLineString(line));
                        break;
                    case Polyline polyline:
                        gjgc.Geometries.Add(
                            new GeoJsonLineString(polyline));
                        break;
                    case Hatch hatch:
                        break;
                    default:
                        break;
                }
            }
            feature.Geometry = gjgc;
            Features.Add(feature);
        }
    }

    public class GeoJsonCRS
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "name";

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; }

        public GeoJsonCRS()
        {
            Properties = new Dictionary<string, object>
            {
                ["name"] = "urn:ogc:def:crs:EPSG::25832"
            };
        }
    }

    public class GeoJsonFeature
    {
        [JsonPropertyName("type")]
        public string Type { get; } = "Feature";

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        [JsonPropertyName("geometry")]
        public GeoJsonGeometry Geometry { get; set; }
    }

    [JsonDerivedType(typeof(GeoJsonPoint))]
    [JsonDerivedType(typeof(GeoJsonLineString))]
    [JsonDerivedType(typeof(GeoJsonPolygon))]
    [JsonDerivedType(typeof(GeoJsonGeometryCollection))]
    public abstract class GeoJsonGeometry
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class GeoJsonPoint : GeoJsonGeometry
    {
        public GeoJsonPoint()
        {
            Type = "Point";
        }

        [JsonPropertyName("coordinates")]
        public double[] Coordinates { get; set; }
    }

    public class GeoJsonLineString : GeoJsonGeometry
    {
        public GeoJsonLineString()
        {
            Type = "LineString";
        }

        [JsonPropertyName("coordinates")]
        public double[][] Coordinates { get; set; }
        public GeoJsonLineString(Line line)
        {
            Coordinates = new double[2][];
            Coordinates[0] = new double[]
                {line.StartPoint.X, line.StartPoint.Y};
            Coordinates[1] = new double[]
                {line.EndPoint.X, line.EndPoint.Y};
        }
        public GeoJsonLineString(Polyline polyline) : this()
        {
            List<Point2d> points = new List<Point2d>();
            int numOfVert = polyline.NumberOfVertices - 1;
            if (polyline.Closed) numOfVert++;
            for (int i = 0; i < numOfVert; i++)
            {
                switch (polyline.GetSegmentType(i))
                {
                    case SegmentType.Line:
                        LineSegment2d ls = polyline.GetLineSegment2dAt(i);
                        if (i == 0)
                        {//First iteration
                            points.Add(ls.StartPoint);
                        }
                        points.Add(ls.EndPoint);
                        break;
                    case SegmentType.Arc:
                        CircularArc2d arc = polyline.GetArcSegment2dAt(i);
                        double sPar = arc.GetParameterOf(arc.StartPoint);
                        double ePar = arc.GetParameterOf(arc.EndPoint);
                        double length = arc.GetLength(sPar, ePar);
                        double radians = length / arc.Radius;
                        int nrOfSamples = (int)(radians / 0.04);
                        if (nrOfSamples < 3)
                        {
                            if (i == 0) points.Add(arc.StartPoint);
                            points.Add(arc.EndPoint);
                        }
                        else
                        {
                            Point2d[] samples = arc.GetSamplePoints(nrOfSamples);
                            if (i != 0) samples = samples.Skip(1).ToArray();
                            foreach (Point2d p2d in samples) points.Add(p2d);
                        }
                        break;
                    case SegmentType.Coincident:
                    case SegmentType.Point:
                    case SegmentType.Empty:
                    default:
                        continue;
                }
            }
            Coordinates = new double[points.Count][];
            for (int i = 0; i < points.Count; i++)
            {
                Coordinates[i] = new double[]
                    {points[i].X, points[i].Y};
            }
        }
        public GeoJsonLineString(ViewFrame viewFrame) : this()
        {
            DBObjectCollection dboc1 = new DBObjectCollection();
            viewFrame.Explode(dboc1);
            foreach (var item in dboc1)
            {
                if (item is BlockReference bref)
                {
                    DBObjectCollection dboc2 = new DBObjectCollection();
                    bref.Explode(dboc2);

                    foreach (var item2 in dboc2)
                    {
                        if (item2 is Polyline pline)
                        {
                            Coordinates = new double[5][];
                            Point3d p;
                            for (int i = 0; i < pline.NumberOfVertices + 1; i++)
                            {
                                switch (i)
                                {
                                    case 4:
                                        p = pline.GetPoint3dAt(0);
                                        break;
                                    default:
                                        p = pline.GetPoint3dAt(i);
                                        break;
                                }
                                Coordinates[i] = new double[] { p.X, p.Y };
                            }
                        }
                    }
                }
            }
        }
    }

    public class GeoJsonPolygon : GeoJsonGeometry
    {
        public GeoJsonPolygon()
        {
            Type = "Polygon";
        }

        [JsonPropertyName("coordinates")]
        public double[][][] Coordinates { get; set; }
    }

    public class GeoJsonGeometryCollection : GeoJsonGeometry
    {
        public GeoJsonGeometryCollection()
        {
            Type = "GeometryCollection";
        }

        [JsonPropertyName("geometries")]
        public List<GeoJsonGeometry> Geometries { get; set; } = new List<GeoJsonGeometry>();
    }
}
