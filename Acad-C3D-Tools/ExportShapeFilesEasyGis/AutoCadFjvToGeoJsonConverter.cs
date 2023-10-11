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
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Dreambuild.AutoCAD;

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
using IntersectUtilities.DynamicBlocks;

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;
using NetTopologySuite.Operation.Union;
using Polygon = NetTopologySuite.Geometries.Polygon;
using NetTopologySuite.Operation.Linemerge;
using EGIS.ShapeFileLib;
using IntersectUtilities;

namespace ExportShapeFiles
{
    public interface IAutoCadFjvToShapeConverter
    {
        ShapeRecord Convert(Entity entity);
    }

    public class ShapeRecord
    {
        public PointD[] Points { get; set; }
        public int NumberOfPoints { get => Points == null ? Points.Length : 0; }
        public string[] Properties { get; set; }
    }

    public class PolylineFjvToShapePolygonConverter : IAutoCadFjvToShapeConverter
    {
        public ShapeRecord Convert(Entity entity)
        {
            if (!(entity is Polyline pl))
                throw new ArgumentException($"Entity {entity.Handle} is not a polyline!");

            string color;
            switch (GetPipeType(pl))
            {
                case PipeTypeEnum.Twin:
                    color = "#FF00FF";
                    break;
                case PipeTypeEnum.Frem:
                    color = "#FF0000";
                    break;
                case PipeTypeEnum.Retur:
                    color = "#0000FF";
                    break;
                case PipeTypeEnum.Enkelt:
                default:
                    throw new System.Exception(
                        $"{GetPipeType(pl)} of {pl.Handle} is not supported.");
            }

            string[] props = new string[10]
            {
                "",
                GetPipeSystem(pl).ToString(),
                "",
                GetPipeType(pl).ToString(),
                GetPipeDN(pl).ToString("000"),
                "",
                GetPipeSeriesV2(pl).ToString(),
                "",
                GetPipeKOd(pl).ToString(),
                color
            };

            if (pl.Closed) throw new System.NotSupportedException(
                $"Polyline {pl.Handle} is closed! Closed polylines are not supported yet!");
            if (pl.Length < 0.1) throw new System.NotSupportedException(
                $"Polyline {pl.Handle} is too short! Polylines shorter than 0.1m are not allowed!");

            var samplePoints = pl.GetSamplePoints();

            List<Point2d> fsPoints = new List<Point2d>();
            List<Point2d> ssPoints = new List<Point2d>();

            double halfKOd = GetPipeKOd(pl, true) / 1000.0 / 2;

            for (int i = 0; i < samplePoints.Count; i++)
            {
                try
                {
                    Point3d samplePoint = samplePoints[i].To3D();
                    var v = pl.GetFirstDerivative(
                        pl.GetClosestPointTo(samplePoint, false))
                        .GetPerpendicularVector().GetNormal();

                    fsPoints.Add((samplePoint + v * halfKOd).To2D());
                    ssPoints.Add((samplePoint + v * -halfKOd).To2D());
                }
                catch (System.Exception)
                {
                    prdDbg($"Getting derivative failed at:\n");
                    prdDbg($"Polyline: {pl.Handle}\n");
                    prdDbg($"Sample point: {samplePoints[i]}\n");
                    throw;
                }
            }

            List<Point2d> points = new List<Point2d>();
            points.AddRange(fsPoints);
            ssPoints.Reverse();
            points.AddRange(ssPoints);
            points.Add(fsPoints[0]);
            
            ShapeRecord record = new ShapeRecord();
            record.Points = points.Select(x => new PointD(x.X, x.Y)).ToArray();
            record.Properties = props;
            return record;
        }
    }

    public class BlockFjvToShapeConverter : IAutoCadFjvToShapeConverter
    {
        public ShapeRecord Convert(Entity entity)
        {
            if (!(entity is BlockReference br))
                throw new ArgumentException($"Entity {entity.Handle} is not a block!");

            System.Data.DataTable dt = ExportShapeFilesEasyGis.Utils.GetFjvBlocksDt();
            Transaction tx = br.Database.TransactionManager.TopTransaction;

            string color = "#000000";
            string[] props = new string[10]
            {
                br.RealName(),
                ComponentSchedule.ReadComponentType(br, dt),
                ComponentSchedule.ReadBlockRotation(br, dt).ToString("0.00"),
                ComponentSchedule.ReadComponentSystem(br, dt),
                ComponentSchedule.ReadComponentDN1(br, dt),
                ComponentSchedule.ReadComponentDN2(br, dt),
                PropertyReader.ReadComponentSeries(br, dt),
                ComponentSchedule.ReadComponentVinkel(br, dt),
                "",
                color,
            };

            string realName = br.RealName();

            BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);

            List<Geometry> geomsToMerge = new List<Geometry>();

            //Handle the collection of Lines
            //The purpose is to join all lines to polylines
            HashSet<Line> lines = new HashSet<Line>();

            foreach (Oid id in btr)
            {
                Entity member = id.Go<Entity>(tx);
                if (member == null) continue;

                switch (member)
                {
                    case Arc arcOriginal:
                        {
                            Arc arc = (Arc)arcOriginal.Clone();
                            arc.CheckOrOpenForWrite();
                            arc.TransformBy(br.BlockTransform);

                            List<Coordinate> Coordinates = new List<Coordinate>();
                            double length = arc.Length;
                            double radians = length / arc.Radius;
                            int nrOfSamples = (int)(radians / 0.1);
                            if (nrOfSamples < 3)
                            {
                                Coordinates.Add(new Coordinate(arc.StartPoint.X, arc.StartPoint.Y));
                                Coordinates.Add(new Coordinate(arc.GetPointAtDist(arc.Length / 2).X, arc.GetPointAtDist(arc.Length / 2).Y));
                                Coordinates.Add(new Coordinate(arc.EndPoint.X, arc.EndPoint.Y));
                            }
                            else
                            {
                                Curve3d geCurve = arc.GetGeCurve();
                                PointOnCurve3d[] samples = geCurve.GetSamplePoints(nrOfSamples);
                                for (int i = 0; i < samples.Length; i++)
                                    Coordinates.Add(new Coordinate(samples[i].Point.X, samples[i].Point.Y));
                            }

                            LineString lineString = new LineString(Coordinates.ToArray());
                        }
                        continue;
                    case Line lineOriginal:
                        {
                            FlexDataStore fds = lineOriginal.Id.FlexDataStore();
                            if (fds != null && fds.GetValue("IsConstructionLine") == "True")
                                continue;
                            Line line = (Line)lineOriginal.Clone();
                            line.CheckOrOpenForWrite();
                            line.TransformBy(br.BlockTransform);
                            lines.Add(line);
                        }
                        continue;
                    case Polyline polylineOrigianl:
                        {
                            Polyline polyline = (Polyline)polylineOrigianl.Clone();
                            polyline.CheckOrOpenForWrite();
                            polyline.TransformBy(br.BlockTransform);
                            List<Coordinate> Coordinates = new List<Coordinate>();
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
                                        int nrOfSamples = (int)(radians / 0.1);
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

                            LineString lineString = new LineString(
                                points.Select(x => new Coordinate(x.X, x.Y)).ToArray());

                            geomsToMerge.Add(lineString);
                        }
                        continue;
                    case BlockReference nestedBrOriginal:
                        {
                            if (!nestedBrOriginal.RealName().StartsWith("MuffeIntern"))
                                throw new System.Exception(
                                    $"Unhandled block name {nestedBrOriginal.RealName()} " +
                                    $"encountered in block {br.RealName()}.");

                            BlockReference nestedBr = (BlockReference)nestedBrOriginal.Clone();
                            nestedBr.CheckOrOpenForWrite();
                            nestedBr.TransformBy(br.BlockTransform);

                            List<double[][]> coordinatesGatherer = new List<double[][]>();

                            int nrOfSamples = (int)(2 * Math.PI / 0.1);
                            Point2dCollection points = new Point2dCollection(nrOfSamples);

                            Circle circle = new Circle(
                                nestedBr.Position,
                                new Vector3d(0, 0, 1), 0.22);
                            Curve3d curve = circle.GetGeCurve();

                            var samplePs = curve.GetSamplePoints(nrOfSamples).ToList();
                            samplePs.Add(samplePs[0]);
                            foreach (PointOnCurve3d item in samplePs)
                            {
                                Point3d p3d = item.GetPoint();
                                points.Add(new Point2d(p3d.X, p3d.Y));
                            }

                            LinearRing shell = new LinearRing(samplePs.Select(
                                x => new Coordinate(x.Point.X, x.Point.Y)).ToArray());
                            Polygon polygon = new Polygon(shell);

                            geomsToMerge.Add(polygon);
                        }
                        continue;
                    case Hatch hatchOriginal:
                        {
                            Hatch hatch = (Hatch)hatchOriginal.Clone();
                            hatch.CheckOrOpenForWrite();
                            hatch.TransformBy(br.BlockTransform);
                            List<Coordinate> Coordinates = new List<Coordinate>();
                            for (int i = 0; i < hatch.NumberOfLoops; i++)
                            {
                                HatchLoop loop;
                                try
                                {
                                    loop = hatch.GetLoopAt(i);
                                }
                                catch (System.ArgumentException)
                                {
                                    continue;
                                }

                                if (loop.IsPolyline)
                                {
                                    List<BulgeVertex> bvc = loop.Polyline.ToList();
                                    var pointsBvc = bvc.GetSamplePoints();
                                    for (int j = 0; j < pointsBvc.Count; j++)
                                        Coordinates.Add(new Coordinate(pointsBvc[j].X, pointsBvc[j].Y));
                                }
                                else
                                {
                                    HashSet<Point2d> points = new HashSet<Point2d>(
                                        new Point2dEqualityComparer());

                                    Curve2dCollection curves = loop.Curves;
                                    foreach (Curve2d curve in curves)
                                    {
                                        switch (curve)
                                        {
                                            case LineSegment2d l2d:
                                                points.Add(l2d.StartPoint);
                                                points.Add(l2d.EndPoint);
                                                continue;
                                            case CircularArc2d ca2d:
                                                double sPar = ca2d.GetParameterOf(ca2d.StartPoint);
                                                double ePar = ca2d.GetParameterOf(ca2d.EndPoint);
                                                double length = ca2d.GetLength(sPar, ePar);
                                                double radians = length / ca2d.Radius;
                                                int nrOfSamples = (int)(radians / 0.1);
                                                if (nrOfSamples < 3)
                                                {
                                                    points.Add(ca2d.StartPoint);
                                                    points.Add(ca2d.GetSamplePoints(3)[1]);
                                                    points.Add(ca2d.EndPoint);
                                                }
                                                else
                                                {
                                                    Point2d[] samples = ca2d.GetSamplePoints(nrOfSamples);
                                                    foreach (Point2d p2d in samples)
                                                        points.Add(p2d);
                                                }
                                                continue;
                                            default:
                                                prdDbg("(WRN:2023:1) Non handled type " + curve);
                                                break;
                                        }
                                    }

                                    var pointsBvc = points.SortAndEnsureCounterclockwiseOrder();
                                    for (int j = 0; j < pointsBvc.Count; j++)
                                        Coordinates.Add(new Coordinate(pointsBvc[j].X, pointsBvc[j].Y));
                                }
                            }
                            
                            Coordinates.Add(new Coordinate(Coordinates[0].X, Coordinates[0].Y));
                            LinearRing shell = new LinearRing(Coordinates.ToArray());
                            Polygon polygon = new Polygon(shell);
                            
                            geomsToMerge.Add(polygon);
                        }
                        continue;
                    case AttributeDefinition atrDef:
                    case DBText text:
                    case DBPoint point:
                        continue;
                    default:
                        prdDbg("(WRN:2023:2) Non handled type " + entity);
                        break;
                }
            }

            //Convert all objects to polygons
            var geometryFactory = new GeometryFactory();

            var polygons = new List<Polygon>();

            foreach (Geometry geometry in geomsToMerge)
            {
                if (geometry is LineString ls)
                {
                    var buffer = ls.Buffer(0.05, EndCapStyle.Flat);  // Adjust buffer distance as needed

                    // Add the buffered Polygon to the list
                    polygons.Add((Polygon)buffer);
                }
                else if (geometry is Polygon polygon)
                {
                    polygons.Add(polygon);
                }
                else
                {
                    prdDbg("(WRN:2023:3) Non handled type " + geometry.GeometryType);
                }
            }

            #region Separately handle lines
            if (lines.Count != 0)
            {
                //Now convert lines to linestrings and merge them
                // Convert them to NTS LineStrings
                List<LineString> lineStrings = new List<LineString>();
                foreach (var cadLine in lines)
                {
                    Coordinate[] coordinates = new Coordinate[]
                    {
                    new Coordinate(cadLine.StartPoint.X, cadLine.StartPoint.Y),
                    new Coordinate(cadLine.EndPoint.X, cadLine.EndPoint.Y)
                    };
                    lineStrings.Add(new LineString(coordinates));
                }

                LineMerger lm = new LineMerger();
                lm.Add(lineStrings);
                var mergedLs = lm.GetMergedLineStrings();

                foreach (var mls in mergedLs)
                {
                    Polygon polygon = (Polygon)mls.Buffer(0.05, EndCapStyle.Flat);
                    polygons.Add(polygon);
                }
            }
            #endregion

            //Now do the final merging
            {
                // Merge the polygons into a single polygon
                var union = CascadedPolygonUnion.Union(polygons.ToArray());

                ShapeRecord feature = new ShapeRecord();
                feature.Points = union.Coordinates.Select(x => new PointD(x.X, x.Y)).ToArray();
                feature.Properties = props;

                return feature;
            }
        }
    }

    public static class FjvToShapeConverterFactory
    {
        public static IAutoCadFjvToShapeConverter CreateConverter(Entity entity)
        {
            switch (entity)
            {
                case BlockReference _:
                    return new BlockFjvToShapeConverter();
                case Polyline _:
                    return new PolylineFjvToShapePolygonConverter();
                default:
                    prdDbg($"Unsupported AutoCAD entity type {entity.GetType()} encountered.");
                    return null;
                    //throw new NotSupportedException("Unsupported AutoCAD entity type.");
            }
        }
    }
}
