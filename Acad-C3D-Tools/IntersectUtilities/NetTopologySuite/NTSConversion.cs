using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.UtilsCommon;
using NetTopologySuite.Geometries;

namespace IntersectUtilities.NTS
{
    public static class NTSConversion
    {
        public static Polygon ConvertClosedPlineToNTSPolygon(Polyline pline)
        {
            var points = new List<Coordinate>();
            for (int i = 0; i < pline.NumberOfVertices; i++)
                points.Add(new Coordinate(pline.GetPoint2dAt(i).X, pline.GetPoint2dAt(i).Y));
            points.Add(new Coordinate(pline.GetPoint3dAt(0).X, pline.GetPoint3dAt(0).Y));
            return new Polygon(new LinearRing(points.ToArray()));
        }
        public static Polygon ConvertClosedPlineToNTSPolygonWithCurveApproximation(Polyline pline)
        {
            if (!pline.Closed) throw new System.Exception($"Polyline {pline.Handle} is not closed!");

            //Approximation setting
            double epsilon = 0.01;

            var points = new List<Coordinate>();
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                switch (pline.GetSegmentType(i))
                {
                    case SegmentType.Line:
                        points.Add(pline.GetPoint2dAt(i).GetCoordinate());
                        break;
                    case SegmentType.Arc:
                        CircularArc2d arc = pline.GetArcSegment2dAt(i);

                        double angle = Math.Abs(arc.EndAngle - arc.StartAngle);
                        double safe = 2 * Math.Acos(1 - epsilon / arc.Radius);
                        int nrOfSamples = (int)Math.Ceiling(angle / safe);

                        if (nrOfSamples < 3)
                        {
                            points.Add(pline.GetPoint2dAt(i).GetCoordinate());
                        }
                        else
                        {
                            Point2d[] samples = arc.GetSamplePoints(nrOfSamples);                            
                            foreach (Point2d p2d in samples.SkipLast(1)) points.Add(p2d.GetCoordinate());
                        }
                        break;
                    case SegmentType.Coincident:
                    case SegmentType.Point:
                    case SegmentType.Empty:
                    default:
                        break;
                }
            }

            points.Add(pline.GetPoint2dAt(0).GetCoordinate());
            return new Polygon(new LinearRing(points.ToArray()));
        }
        public static LineString ConvertPlineToNTSLineString(Polyline pline)
        {
            var points = new List<Coordinate>();
            var samplePoints = pline.GetSamplePoints();
            foreach (var samplePoint in samplePoints)
                points.Add(new Coordinate(samplePoint.X, samplePoint.Y));
            return new LineString(points.ToArray());
        }
        public static Polyline ConvertNTSLineStringToPline(LineString lineString)
        {
            Polyline polyline = new Polyline();

            for (int i = 0; i < lineString.Coordinates.Length; i++)
            {
                Coordinate coord = lineString.Coordinates[i];
                polyline.AddVertexAt(i, new Point2d(coord.X, coord.Y), 0, 0, 0);
            }

            return polyline;
        }
        public static Point ConvertBrToNTSPoint(BlockReference br)
        {
            return new Point(br.Position.X, br.Position.Y);
        }
        public static Hatch ConvertNTSPolygonToHatch(Polygon poly)
        {
            var points = poly.Coordinates.Select(x => x.GetPoint2d()).ToArray();

            Point2dCollection points2d = new Point2dCollection();
            DoubleCollection dc = new DoubleCollection();
            for (int i = 0; i < points.Length; i++)
            {
                points2d.Add(points[i]);
                dc.Add(0.0);
            }

            Hatch hatch = new Hatch();
            hatch.Normal = new Vector3d(0.0, 0.0, 1.0);
            hatch.Elevation = 0.0;
            hatch.PatternScale = 1.0;
            hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            //Maybe append to database here
            hatch.AppendLoop(HatchLoopTypes.Default, points2d, dc);
            hatch.EvaluateHatch(true);

            return hatch;
        }
        public static MPolygon ConvertNTSPolygonToMPolygon(Polygon poly)
        {
            var points = poly.Coordinates.Select(x => x.GetPoint2d()).ToArray();

            Polyline polyline = new Polyline(points.Length);
            foreach (var point in points)
                polyline.AddVertexAt(polyline.NumberOfVertices, point, 0, 0, 0);
            polyline.Closed = true;

            MPolygon mpg = new MPolygon();
            mpg.AppendLoopFromBoundary(polyline, true, Tolerance.Global.EqualPoint);
            
            return mpg;
        }
        public static Polygon ConvertMPolygonToNTSPolygon(MPolygon mpg)
        {
            int loops = mpg.NumMPolygonLoops;
            if (loops > 1) throw new System.Exception($"MPolygon {mpg.Handle} has more than one loop!");
            List<Coordinate> coords = new List<Coordinate>();
            for (int i = 0; i < loops; i++)
            {
                MPolygonLoop loop = mpg.GetMPolygonLoopAt(i);
                foreach (BulgeVertex bv in loop)
                    coords.Add(new Coordinate(bv.Vertex.X, bv.Vertex.Y));
            }

            Polygon pgn = new Polygon(new LinearRing(coords.ToArray()));
            return pgn;
        }
        public static void ConvertNTSMultiPointToDBPoints(Geometry mp, Database db)
        {
            var pm = new ProgressMeter();
            pm.Start("Creating DBPoints");
            pm.SetLimit(mp.NumPoints);
            for (int i = 0; i < mp.NumPoints; i++)
            {
                Coordinate co = mp.Coordinates[i];

                var point = new DBPoint(new Point3d(co.X, co.Y, 0));
                point.AddEntityToDbModelSpace(db);
                pm.MeterProgress();
            }
            pm.Stop();
        }
        public static Coordinate[][] GetClusteredCoordinates(double[][] data, int K, int[] clustering)
        {
            // Step 1: Count the number of points in each cluster
            int[] clusterCounts = new int[K];
            for (int i = 0; i < clustering.Length; ++i)
            {
                int clusterIndex = clustering[i];
                clusterCounts[clusterIndex]++;
            }

            // Step 2: Create arrays for each cluster
            Coordinate[][] clusteredCoordinates = new Coordinate[K][];
            for (int k = 0; k < K; ++k)
            {
                clusteredCoordinates[k] = new Coordinate[clusterCounts[k]];
            }

            // Step 3: Reset cluster counts for indexing
            for (int k = 0; k < K; ++k)
            {
                clusterCounts[k] = 0;
            }

            // Step 4: Fill the arrays with coordinates
            int n = data.Length;
            for (int i = 0; i < n; ++i)  // Process each data point
            {
                int k = clustering[i];  // Cluster for current data point
                                        // Assuming each data point is a 2D point
                Coordinate coord = new Coordinate(data[i][0], data[i][1]);
                int index = clusterCounts[k];
                clusteredCoordinates[k][index] = coord;
                clusterCounts[k]++;
            }

            return clusteredCoordinates;  // Array of arrays of coordinates, organized by cluster
        }
    }

    public static class NTSExtensions
    {
        public static Point3d GetPoint3d(this Coordinate coord) => new Point3d(coord.X, coord.Y, coord.Z);
        public static Point2d GetPoint2d(this Coordinate coord) => new Point2d(coord.X, coord.Y);
        public static Coordinate GetCoordinate(this Point2d point) => new Coordinate(point.X, point.Y);
        public static Coordinate GetCoordinate(this Point3d point) => new Coordinate(point.X, point.Y);
    }
}
