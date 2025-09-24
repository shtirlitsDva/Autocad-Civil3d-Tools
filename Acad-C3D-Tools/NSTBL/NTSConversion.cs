using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;

namespace IntersectUtilities.NSTBL
{
    internal static class NTSConversion
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
    }
}
