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
//using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

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
        Point3d[] Get3DPoints();
    }

    public interface IEntityCreator
    {
        Oid CreateEntity(Database database);
    }

    public static class Helper
    {
        public static Regex point3DParser =
            new Regex(@"(?<X>-?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s(?<Y>-?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s(?<Z>-?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)");

        public static Regex point2DParser =
            new Regex(@"(?<X>(-?(?:\d+)(?:\.(?:\d+)?)?))\s(?<Y>(-?(?:\d+)(?:\.(?:\d+)?)?))");
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
    [XmlRootAttribute("MultiCurve", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class MultiCurveType : AbstractGeometricAggregateType, IPointParser
    {
        public Point3d[] Get3DPoints()
        {
            Point3d[] points = new Point3d[0];

            foreach (CurvePropertyType curveType in this.curveMember)
            {
                switch (curveType.AbstractCurve)
                {
                    case CompositeCurveType cct:
                        throw new NotImplementedException();
                    case CurveType ct:
                        throw new NotImplementedException();
                    case LineStringType lst:
                        IPointParser pointParser = lst as IPointParser;
                        points = points.ConcatAr(pointParser.Get3DPoints());
                        break;
                    case OrientableCurveType oct:
                        throw new NotImplementedException();
                    default:
                        break;
                }
            }

            return points;
        }
    }

    [XmlRootAttribute("LineString", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class LineStringType : AbstractCurveType, IPointParser, IEntityCreator
    {
        public Oid CreateEntity(Database database)
        {
            //This interface implementation was added because a component failed to be created
            //and the error was that the component did not implement IEntityCreator
            //For ledninger the system is different
            if (this.Items.Length < 1)
            {
                Log.log($"ADVARSEL! Element id {this.GMLTypeID} har ikke noget geometri! Springer over!");
                return Oid.Null;
            }

            Point3d[] points = Get3DPoints();
            Polyline polyline = new Polyline(points.Length);

            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(polyline.NumberOfVertices, points[i].To2D(), 0, 0, 0);

            Oid oid = polyline.AddEntityToDbModelSpace(database);

            return oid;
        }

        public Point3d[] Get3DPoints()
        {
            Point3d[] points = new Point3d[0];
            foreach (IPointParser pointParser in this.Items) points = points.ConcatAr(pointParser.Get3DPoints());
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
        public Point3d[] Get3DPoints()
        {
            //Guard against badly formatted strings
            if (!Helper.point3DParser.IsMatch(Text))
                throw new System.Exception($"Text string {Text} was not correctly formatted as a list of 3D points!");

            MatchCollection matches = Helper.point3DParser.Matches(Text);
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

        public Point2d[] Get2DPoints()
        {
            //Guard against badly formatted strings
            if (!Helper.point2DParser.IsMatch(Text))
                throw new System.Exception($"Text string {Text} was not correctly formatted as a list of 2D points!");

            MatchCollection matches = Helper.point2DParser.Matches(Text);
            Point2d[] points = new Point2d[matches.Count];

            for (int i = 0; i < matches.Count; i++)
            {
                GroupCollection groups = matches[i].Groups;
                double X = Convert.ToDouble(groups["X"].Captures[0].Value);
                double Y = Convert.ToDouble(groups["Y"].Captures[0].Value);
                points[i] = new Point2d(X, Y);
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
        /// <summary>
        /// Can only be used when Text contains only one number
        /// for example in bundkote for afløbskomponenter
        /// </summary>
        public double GetDouble() => Convert.ToDouble(Text);
        public Point3d[] Get3DPoints()
        {
            //This class only has one point

            //Guard against badly formatted strings
            if (!Helper.point3DParser.IsMatch(Text))
                throw new System.Exception($"Text string {Text} was not correctly formatted as a point!");

            Match match = Helper.point3DParser.Match(Text);
            GroupCollection groups = match.Groups;
            double X = Convert.ToDouble(groups["X"].Captures[0].Value);
            double Y = Convert.ToDouble(groups["Y"].Captures[0].Value);
            double Z = Convert.ToDouble(groups["Z"].Captures[0].Value);
            return new Point3d[] { new Point3d(X, Y, Z) };
        }
    }
    [XmlRootAttribute("Point", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class PointType : AbstractGeometricPrimitiveType, IEntityCreator, IPointParser
    {
        public Oid CreateEntity(Database database)
        {
            switch (this.Item)
            {
                case DirectPositionType dpt:
                    var point = dpt.Get3DPoints();
                    DBPoint dBp = new DBPoint(point.First());
                    return dBp.AddEntityToDbModelSpace(database);
                default:
                    throw new System.Exception($"Unexpected type in PointType.Item: {this.Item.GetType().Name}");
            }
        }

        public Point3d[] Get3DPoints()
        {
            switch (this.Item)
            {
                case DirectPositionType dpt:
                    return dpt.Get3DPoints();
                default:
                    throw new System.Exception(
                        $"Unexpected type in PointType.Item: {this.Item.GetType().Name}" +
                        $"GMLTypeID: {this.GMLTypeID}");
            }
        }
    }
    [XmlRootAttribute("geometryMember", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class GeometryPropertyType
    {
        [XmlElement("Polygon", typeof(PolygonType))]
        [XmlElement("Point", typeof(PointType))]
        [XmlElement("Surface", typeof(SurfaceType))]
        [XmlElement("MultiSurface", typeof(MultiSurfaceType))]
        [XmlElement("LineString", typeof(LineStringType))]
        [XmlElement("CompositeCurve", typeof(CompositeCurveType))]
        [XmlElement("Curve", typeof(CurveType))]
        [XmlElement("OrientableCurve", typeof(OrientableCurveType))]
        public AbstractGeometryType Item { get; set; }
    }
    [XmlRootAttribute("AbstractGeometry", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class AbstractGeometryType { }
    [XmlRootAttribute("Surface", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class SurfaceType : IEntityCreator
    {
        public Oid CreateEntity(Database database)
        {
            if (this.patches.AbstractSurfacePatch == null)
            {
                switch (this.patches.patch)
                {
                    case PolygonPatchType ppt:
                        var exterior = ppt.exterior;
                        var ringType = exterior.Item;

                        switch (ringType)
                        {
                            case LinearRingType lrt:
                                DirectPositionListType dplt;
                                if ((dplt = lrt.Items[0] as DirectPositionListType) != null)
                                {
                                    var points = dplt.Get3DPoints();

                                    Point2dCollection points2d = new Point2dCollection();
                                    DoubleCollection dc = new DoubleCollection();
                                    //-1 beacuse the last point is a repetition of first
                                    for (int i = 0; i < points.Length; i++)
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
                    default:
                        throw new NotImplementedException($"Type {this.patches.patch.GetType()} is not implemented in patches!");
                }
            }
            else
            {
                if (this.patches.AbstractSurfacePatch.Length > 1) throw
                        new System.Exception("AbstractSurfacePatch.Length is larger than 1! Not implemented.");

                if (this.patches.AbstractSurfacePatch.Any(x => x is PolygonPatchType))
                {
                    PolygonPatchType ppt = (PolygonPatchType)
                        this.patches.AbstractSurfacePatch.Where(x => x is PolygonPatchType).FirstOrDefault();

                    var exterior = ppt.exterior;
                    var ringType = exterior.Item;

                    switch (ringType)
                    {
                        case LinearRingType lrt:
                            DirectPositionListType dplt;
                            if ((dplt = lrt.Items[0] as DirectPositionListType) != null)
                            {
                                var points = dplt.Get3DPoints();

                                Point2dCollection points2d = new Point2dCollection();
                                DoubleCollection dc = new DoubleCollection();
                                //-1 beacuse the last point is a repetition of first
                                for (int i = 0; i < points.Length; i++)
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
                else
                {
                    throw new NotImplementedException("Surface does not implement specified type!");
                }
            }
        }
    }
    public partial class MultiSurfaceType : IEntityCreator
    {
        public Oid CreateEntity(Database database)
        {
            if (this.surfaceMember != null && this.surfaceMember.Length > 0)
            {
                Hatch hatch = new Hatch();
                hatch.Normal = new Vector3d(0.0, 0.0, 1.0);
                hatch.Elevation = 0.0;
                hatch.PatternScale = 1.0;
                hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                Oid hatchId = hatch.AddEntityToDbModelSpace(database);

                foreach (SurfacePropertyType spt in surfaceMember)
                {
                    switch (spt.AbstractSurface)
                    {
                        case PolygonType pt:
                            {
                                var exterior = pt.exterior;
                                var ringType = exterior.Item;
                                switch (ringType)
                                {
                                    case LinearRingType lrt:
                                        DirectPositionListType dplt;
                                        if ((dplt = lrt.Items[0] as DirectPositionListType) != null)
                                        {
                                            var points = dplt.Get3DPoints();

                                            Point2dCollection points2d = new Point2dCollection();
                                            DoubleCollection dc = new DoubleCollection();
                                            //-1 beacuse the last point is a repetition of first
                                            for (int i = 0; i < points.Length; i++)
                                            {
                                                points2d.Add(points[i].To2D());
                                                dc.Add(0.0);
                                            }

                                            hatch.AppendLoop(HatchLoopTypes.Default, points2d, dc);
                                            hatch.EvaluateHatch(true);
                                        }
                                        else throw new System.Exception(
                                            $"Unexpected type in PolygonType.exterior.Item.Items[0]: {lrt.Items[0].GetType().Name}");
                                        break;
                                    case RingType rt:
                                        throw new System.NotImplementedException();
                                    default:
                                        throw new System.Exception($"Unexpected type in PolygonType.exterior.Item: {ringType.GetType().Name}");
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException(
                                $"Element {this.GMLTypeID} asked for non implemented geometry! Error 124385.");
                    }
                }

                return hatchId;
            }
            else throw new NotImplementedException(
                $"Unexpected geometry in element {this.GMLTypeID}!");
        }
    }
    /// <summary>
    /// gml:SurfacePatchArrayPropertyType is a container for a sequence of surface patches.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.8.4084.0")]
    [Serializable]
    [DesignerCategoryAttribute("code")]
    [XmlTypeAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
    public partial class SurfacePatchArrayPropertyType
    {
        //[XmlElement("AbstractSurfacePatch")]
        [XmlArrayItem(typeof(AbstractParametricCurveSurfaceType), Namespace = "http://www.opengis.net/gml/3.2", ElementName = "AbstractParametricCurveSurface")]
        [XmlArrayItem(typeof(AbstractGriddedSurfaceType), Namespace = "http://www.opengis.net/gml/3.2", ElementName = "AbstractGriddedSurface")]
        [XmlArrayItem(typeof(SphereType), Namespace = "http://www.opengis.net/gml/3.2", ElementName = "Sphere")]
        [XmlArrayItem(typeof(CylinderType), Namespace = "http://www.opengis.net/gml/3.2", ElementName = "Cylinder")]
        [XmlArrayItem(typeof(ConeType), Namespace = "http://www.opengis.net/gml/3.2", ElementName = "Cone")]
        [XmlArrayItem(typeof(RectangleType), Namespace = "http://www.opengis.net/gml/3.2", ElementName = "Rectangle")]
        [XmlArrayItem(typeof(TriangleType), Namespace = "http://www.opengis.net/gml/3.2", ElementName = "Triangle")]
        [XmlArrayItem(typeof(PolygonPatchType), Namespace = "http://www.opengis.net/gml/3.2", ElementName = "PolygonPatch")]
        public AbstractSurfacePatchType[] AbstractSurfacePatch { get; set; }

        [XmlElement(typeof(AbstractParametricCurveSurfaceType))]
        [XmlElement(typeof(AbstractGriddedSurfaceType))]
        [XmlElement("Sphere", typeof(SphereType))]
        [XmlElement("Cylinder", typeof(CylinderType))]
        [XmlElement("Cone", typeof(ConeType))]
        [XmlElement("Rectangle", typeof(RectangleType))]
        [XmlElement("Triangle", typeof(TriangleType))]
        [XmlElement("PolygonPatch", typeof(PolygonPatchType))]
        public AbstractSurfacePatchType patch { get; set; }
    }
    [XmlRootAttribute("Polygon", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class PolygonType : IEntityCreator, IPointParser
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
                        var points = dplt.Get3DPoints();

                        Point2dCollection points2d = new Point2dCollection();
                        DoubleCollection dc = new DoubleCollection();
                        //-1 beacuse the last point is a repetition of first
                        for (int i = 0; i < points.Length; i++)
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

        public Point3d[] Get3DPoints()
        {
            var exterior = this.exterior;
            var ringType = exterior.Item;

            switch (ringType)
            {
                case LinearRingType lrt:
                    DirectPositionListType dplt;
                    if ((dplt = lrt.Items[0] as DirectPositionListType) != null)
                    {
                        return dplt.Get3DPoints();
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
    public partial class SurfacePropertyType
    {
        [XmlElement("Polygon", typeof(PolygonType))]
        public AbstractSurfaceType AbstractSurface { get; set; }
    }
}
