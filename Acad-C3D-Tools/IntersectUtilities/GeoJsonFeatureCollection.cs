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
