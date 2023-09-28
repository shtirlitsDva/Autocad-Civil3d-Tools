using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

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
    }

    public static class NTSExtensions
    {
        public static Point3d GetPoint3d(this Coordinate coord) => new Point3d(coord.X, coord.Y, coord.Z);
        public static Point2d GetPoint2d(this Coordinate coord) => new Point2d(coord.X, coord.Y);
    }
}
