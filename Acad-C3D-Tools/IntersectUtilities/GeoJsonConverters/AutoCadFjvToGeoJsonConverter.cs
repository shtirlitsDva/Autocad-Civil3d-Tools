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
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
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
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Dreambuild.AutoCAD;
using FolderSelect;

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
using IntersectUtilities.DynamicBlocks;

using Autodesk.Aec.DatabaseServices;

namespace IntersectUtilities
{
    public interface IAutoCadFjvToGeoJsonConverter
    {
        IEnumerable<GeoJsonFeature> Convert(Entity entity);
    }

    public class PolylineFjvToGeoJsonPolygonConverter : IAutoCadFjvToGeoJsonConverter
    {
        public IEnumerable<GeoJsonFeature> Convert(Entity entity)
        {
            if (!(entity is Polyline pl))
                throw new ArgumentException($"Entity {entity.Handle} is not a polyline!");

            var feature = new GeoJsonFeature
            {
                Properties = new Dictionary<string, object>
                {
                    { "DN", GetPipeDN(pl) },
                    { "System", GetPipeType(pl) },
                    { "Serie", GetPipeSeriesV2(pl) },
                    { "Type", GetPipeSystem(pl) },
                    { "KOd", GetPipeKOd(pl) },
                },

                Geometry = new GeoJsonPolygon() { },
            };

            switch (GetPipeType(pl))
            {
                case PipeTypeEnum.Twin:
                    feature.Properties.Add("color", "#FF00FF");
                    break;
                case PipeTypeEnum.Frem:
                    feature.Properties.Add("color", "#FF0000");
                    break;
                case PipeTypeEnum.Retur:
                    feature.Properties.Add("color", "#0000FF");
                    break;
                case PipeTypeEnum.Enkelt:
                default:
                    throw new System.Exception(
                        $"{GetPipeType(pl)} of {pl.Handle} is not supported.");
            }

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
                Point3d samplePoint = samplePoints[i].To3D();
                var v = pl.GetFirstDerivative(samplePoint)
                    .GetPerpendicularVector().GetNormal();

                fsPoints.Add((samplePoint + v * halfKOd).To2D());
                ssPoints.Add((samplePoint + v * -halfKOd).To2D());
            }

            List<Point2d> points = new List<Point2d>();
            points.AddRange(fsPoints);
            ssPoints.Reverse();
            points.AddRange(ssPoints);
            points.Add(fsPoints[0]);
            //points = points.SortAndEnsureCounterclockwiseOrder();

            List<double[][]> coordinatesGatherer = new List<double[][]>();
            double[][] coordinates = new double[points.Count][];
            for (int i = 0; i < points.Count; i++)
                coordinates[i] = new double[] { points[i].X, points[i].Y };
            coordinatesGatherer.Add(coordinates);
            (feature.Geometry as GeoJsonPolygon).Coordinates = coordinatesGatherer.ToArray();

            yield return feature;
        }
    }

    public class BlockFjvToGeoJsonConverter : IAutoCadFjvToGeoJsonConverter
    {
        public IEnumerable<GeoJsonFeature> Convert(Entity entity)
        {
            if (!(entity is BlockReference br))
                throw new ArgumentException($"Entity {entity.Handle} is not a block!");

            System.Data.DataTable dt = GetFjvBlocksDt();
            Transaction tx = br.Database.TransactionManager.TopTransaction;

            var props = new Dictionary<string, object>
            {
                { "BlockName", br.RealName() },
                { "Type", ComponentSchedule.ReadComponentType(br, dt) },
                { "Rotation", ComponentSchedule.ReadBlockRotation(br, dt).ToString("0.00") },
                { "System", ComponentSchedule.ReadComponentSystem(br, dt) },
                { "DN1", ComponentSchedule.ReadComponentDN1(br, dt) },
                { "DN2", ComponentSchedule.ReadComponentDN2(br, dt) },
                { "Serie", PropertyReader.ReadComponentSeries(br, dt) },
                { "Vinkel", ComponentSchedule.ReadComponentVinkel(br, dt) },
                { "color", "#000000" },
            };

            BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
            

            foreach (Oid id in btr)
            {
                Entity member = id.Go<Entity>(tx);
                if (member == null) continue;

                var feature = new GeoJsonFeature
                {
                    Properties = props,
                };

                switch (member)
                {
                    case Arc arcOriginal:
                        {
                            Arc arc = (Arc)arcOriginal.Clone();
                            arc.CheckOrOpenForWrite();
                            arc.TransformBy(br.BlockTransform);
                            feature.Geometry = new GeoJsonLineString();
                            double[][] Coordinates;
                            double length = arc.Length;
                            double radians = length / arc.Radius;
                            int nrOfSamples = (int)(radians / 0.1);
                            if (nrOfSamples < 3)
                            {
                                Coordinates = new double[3][];
                                Coordinates[0] = new double[]
                                    { arc.StartPoint.X, arc.StartPoint.Y };
                                Coordinates[1] = new double[]
                                    { arc.GetPointAtDist(arc.Length/2).X,
                                    arc.GetPointAtDist(arc.Length/2).Y };
                                Coordinates[2] = new double[]
                                    { arc.EndPoint.X, arc.EndPoint.Y };
                            }
                            else
                            {
                                Curve3d geCurve = arc.GetGeCurve();
                                PointOnCurve3d[] samples = geCurve.GetSamplePoints(nrOfSamples);
                                Coordinates = new double[samples.Length][];
                                for (int i = 0; i < samples.Length; i++)
                                {
                                    Coordinates[i] = new double[2]
                                        {samples[i].Point.X, samples[i].Point.Y};
                                }
                            }
                            ((GeoJsonLineString)feature.Geometry).Coordinates = Coordinates;
                            yield return feature;
                        }
                        continue;
                    case Line lineOriginal:
                        {
                            Line line = (Line)lineOriginal.Clone();
                            line.CheckOrOpenForWrite();
                            line.TransformBy(br.BlockTransform);
                            feature.Geometry = new GeoJsonLineString();
                            double[][] Coordinates;
                            Coordinates = new double[2][];
                            Coordinates[0] = new double[]
                                {line.StartPoint.X, line.StartPoint.Y};
                            Coordinates[1] = new double[]
                                {line.EndPoint.X, line.EndPoint.Y};
                            ((GeoJsonLineString)feature.Geometry).Coordinates = Coordinates;
                            yield return feature;
                        }
                        continue;
                    case Polyline polylineOrigianl:
                        {
                            Polyline polyline = (Polyline)polylineOrigianl.Clone();
                            polyline.CheckOrOpenForWrite();
                            polyline.TransformBy(br.BlockTransform);
                            feature.Geometry = new GeoJsonLineString();
                            double[][] Coordinates;
                            Coordinates = new double[2][];
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
                            Coordinates = new double[points.Count][];
                            for (int i = 0; i < points.Count; i++)
                            {
                                Coordinates[i] = new double[]
                                    {points[i].X, points[i].Y};
                            }
                            ((GeoJsonLineString)feature.Geometry).Coordinates = Coordinates;
                            yield return feature;
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
                            feature.Geometry = new GeoJsonPolygon();
                            List<double[][]> coordinatesGatherer = new List<double[][]>();
                            
                            int nrOfSamples = (int)(2 * Math.PI / 0.1);
                            Point2dCollection points = new Point2dCollection(nrOfSamples);

                            Circle circle = new Circle(
                                nestedBr.Position,
                                new Vector3d(0, 0, 1), 0.22);
                            Curve3d curve = circle.GetGeCurve();

                            var samplePs = curve.GetSamplePoints(nrOfSamples).ToList();
                            samplePs.Add(samplePs[0]);
                            foreach (var item in samplePs)
                            {
                                Point3d p3d = item.GetPoint();
                                points.Add(new Point2d(p3d.X, p3d.Y));
                            }

                            double[][] coordinates = new double[points.Count][];
                            for (int i = 0; i < points.Count; i++)
                                coordinates[i] = new double[] { points[i].X, points[i].Y };
                            coordinatesGatherer.Add(coordinates);
                            (feature.Geometry as GeoJsonPolygon).Coordinates = coordinatesGatherer.ToArray();

                            yield return feature;
                        }
                        continue;
                    case Hatch hatchOriginal:
                        {
                            Hatch hatch = (Hatch)hatchOriginal.Clone();
                            hatch.CheckOrOpenForWrite();
                            hatch.TransformBy(br.BlockTransform);
                            List<double[][]> coordinatesGatherer = new List<double[][]>();
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
                                    double[][] coordinates = new double[pointsBvc.Count][];
                                    for (int j = 0; j < pointsBvc.Count; j++)
                                    {
                                        coordinates[j] = new double[] { pointsBvc[j].X, pointsBvc[j].Y };
                                    }
                                    coordinatesGatherer.Add(coordinates);
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
                                    double[][] coordinates = new double[pointsBvc.Count][];
                                    for (int j = 0; j < pointsBvc.Count; j++)
                                    {
                                        coordinates[j] = new double[] { pointsBvc[j].X, pointsBvc[j].Y };
                                    }
                                    coordinatesGatherer.Add(coordinates);
                                }
                            }
                            feature.Geometry = new GeoJsonPolygon();
                            (feature.Geometry as GeoJsonPolygon).Coordinates = coordinatesGatherer.ToArray();
                            yield return feature;
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
        }
    }

    public static class FjvToGeoJsonConverterFactory
    {
        public static IAutoCadFjvToGeoJsonConverter CreateConverter(Entity entity)
        {
            switch (entity)
            {
                case BlockReference _:
                    return new BlockFjvToGeoJsonConverter();
                case Polyline _:
                    return new PolylineFjvToGeoJsonPolygonConverter();
                default:
                    prdDbg($"Unsupported AutoCAD entity type {entity.GetType()} encountered.");
                    return null;
                    //throw new NotSupportedException("Unsupported AutoCAD entity type.");
            }
        }
    }
}
