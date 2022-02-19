using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Xml.Schema;
using System.ComponentModel;
using System.Xml;
//using MoreLinq;
//using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

//using static IntersectUtilities.Enums;
//using static IntersectUtilities.HelperMethods;
//using static IntersectUtilities.Utils;
//using static IntersectUtilities.PipeSchedule;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Log = LERImporter.SimpleLogger;

namespace LERImporter.Schema
{
    public interface IPointParser
    {
        Point3d[] GetPoints();
    }

    public interface IEntityCreator
    {
        Oid CreateEntity(Database database);
    }

    public static class Helper
    {
        public static Regex pointParser =
            new Regex(@"(?<X>(-?(?:\d+)(?:\.(?:\d+)?)?))\s(?<Y>(-?(?:\d+)(?:\.(?:\d+)?)?))\s(?<Z>(-?(?:\d+)(?:\.(?:\d+)?)?))");
    }

    [XmlRootAttribute("AbstractCurve", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public abstract partial class AbstractCurveType : AbstractGeometricPrimitiveType { }
    [XmlRootAttribute("curveProperty", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class CurvePropertyType
    {
        [XmlElementAttribute("CompositeCurve", typeof(CompositeCurveType))]
        [XmlElementAttribute("Curve", typeof(CurveType))]
        [XmlElementAttribute("LineString", typeof(LineStringType))]
        [XmlElementAttribute("OrientableCurve", typeof(OrientableCurveType))]
        public AbstractCurveType AbstractCurve { get; set; }
    }


    [XmlRootAttribute("LineString", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class LineStringType : AbstractCurveType, IPointParser
    {
        public Point3d[] GetPoints()
        {
            Point3d[] points = new Point3d[0];
            foreach (IPointParser pointParser in this.Items) points = points.ConcatAr(pointParser.GetPoints());
            return points;
        }
    }
    [XmlRootAttribute("CompositeCurve", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class CompositeCurveType : AbstractCurveType { }
    [XmlRootAttribute("Curve", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class CurveType : AbstractCurveType { }
    [XmlRootAttribute("OrientableCurve", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class OrientableCurveType : AbstractCurveType { }
    [XmlRootAttribute("posList", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class DirectPositionListType : IPointParser
    {
        [XmlTextAttribute]
        public string Text { get; set; }
        public Point3d[] GetPoints()
        {
            //Guard against badly formatted strings
            if (!Helper.pointParser.IsMatch(Text))
                throw new System.Exception($"Text string {Text} was not correctly formatted as a point!");

            MatchCollection matches = Helper.pointParser.Matches(Text);
            Point3d[] points = new Point3d[matches.Count];

            for (int i = 0; i < matches.Count; i++)
            {
                GroupCollection groups = matches[i].Groups;
                double X = Convert.ToDouble(groups["X"].Captures[0].Value);
                double Y = Convert.ToDouble(groups["Y"].Captures[0].Value);
                double Z = Convert.ToDouble(groups["Z"].Captures[0].Value);
                points[i] = new Point3d(X, Y, Z);
            }
            return points;
        }
    }
    [XmlRootAttribute("tupleList", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class CoordinatesType { }
    [XmlRootAttribute("pointProperty", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class PointPropertyType
    {
        [XmlElementAttribute("Point", typeof(PointType))]
        public PointType Point { get; set; }
    }
    [XmlRootAttribute("pos", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class DirectPositionType : IPointParser
    {
        [XmlTextAttribute]
        public string Text { get; set; }

        public Point3d[] GetPoints()
        {
            //This class only has one point

            //Guard against badly formatted strings
            if (!Helper.pointParser.IsMatch(Text))
                throw new System.Exception($"Text string {Text} was not correctly formatted as a point!");

            Match match = Helper.pointParser.Match(Text);
            GroupCollection groups = match.Groups;
            double X = Convert.ToDouble(groups["X"].Captures[0].Value);
            double Y = Convert.ToDouble(groups["Y"].Captures[0].Value);
            double Z = Convert.ToDouble(groups["Z"].Captures[0].Value);
            return new Point3d[] { new Point3d(X, Y, Z) };
        }
    }
    [XmlRootAttribute("Point", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class PointType : AbstractGeometricPrimitiveType, IEntityCreator
    {
        public Oid CreateEntity(Database database)
        {
            switch (this.Item)
            {
                case DirectPositionType dpt:
                    var point = dpt.GetPoints();
                    DBPoint dBp = new DBPoint(point.First());
                    return dBp.AddEntityToDbModelSpace(database);
                default:
                    throw new System.Exception($"Unexpected type in PointType.Item: {this.Item.GetType().Name}");
            }
        }
    }
    [XmlRootAttribute("geometryMember", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class GeometryPropertyType
    {
        [XmlElement("Polygon", typeof(PolygonType))]
        [XmlElement("Point", typeof(PointType))]
        public AbstractGeometryType Item { get; set; }
    }
    [XmlRootAttribute("AbstractGeometry", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class AbstractGeometryType { }
    [XmlRootAttribute("Polygon", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class PolygonType : IEntityCreator
    {
        public Oid CreateEntity(Database database)
        {
            var exterior = this.exterior;
            var ringType = exterior.Item;
            switch (ringType)
            {
                case LinearRingType lrt:
                    DirectPositionListType dplt;
                    if ((dplt = lrt.Items[0] as DirectPositionListType) != null)
                    {
                        var points = dplt.GetPoints();

                        Point2dCollection points2d = new Point2dCollection();
                        DoubleCollection dc = new DoubleCollection();
                        //-1 beacuse the last point is a repetition of first
                        for (int i = 0; i < points.Length - 1; i++)
                        {
                            points2d.Add(points[i].To2D());
                            dc.Add(0.0);
                        }

                        //Polyline pline = new Polyline(points.Length);
                        ////-1 beacuse the last point is a repetition of first
                        //for (int i = 0; i < points.Length - 1; i++)
                        //    pline.AddVertexAt(pline.NumberOfVertices, points[i].To2D(), 0, 0, 0);
                        //pline.Closed = true;
                        //Oid plineId = pline.AddEntityToDbModelSpace(database);

                        Hatch hatch = new Hatch();
                        hatch.Normal = new Vector3d(0.0, 0.0, 1.0);
                        hatch.Elevation = 0.0;
                        hatch.PatternScale = 1.0;
                        hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                        Oid hatchId = hatch.AddEntityToDbModelSpace(database);

                        hatch.AppendLoop(HatchLoopTypes.Default, points2d, dc);
                        hatch.EvaluateHatch(true);

                        return hatchId;
                    }
                    else throw new System.Exception(
                        $"Unexpected type in PolygonType.exterior.Item.Items[0]: {lrt.Items[0].GetType().Name}");
                case RingType rt:
                    throw new System.NotImplementedException();
                default:
                    throw new System.Exception($"Unexpected type in PolygonType.exterior.Item: {ringType.GetType().Name}");
            }
        }
    }
    [XmlRootAttribute("exterior", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class AbstractRingPropertyType
    {
        [XmlElementAttribute("LinearRing", typeof(LinearRingType))]
        [XmlElementAttribute("Ring", typeof(RingType))]
        public AbstractRingType Item { get; set; }
    }
    [XmlRootAttribute("LinearRing", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class LinearRingType { }
    [XmlRootAttribute("Ring", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class RingType { }
}
