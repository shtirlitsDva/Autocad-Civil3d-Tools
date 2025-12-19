using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using NetTopologySuite.Geometries;

namespace Ler2PolygonSplitting.NTS
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

            if (mpg.Area < 0)
            {
                mpg = new MPolygon();
                polyline.ReverseCurve();
                mpg.AppendLoopFromBoundary(polyline, true, Tolerance.Global.EqualPoint);
            }
            
            return mpg;
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
    }
}
