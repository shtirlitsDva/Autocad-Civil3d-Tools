using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial
{
    internal static class Geometry2D
    {
        public static bool OnSegment(Point2d a, Point2d b, Point2d p, double eps)
        {
            return Math.Abs(Cross(a, b, p)) <= eps &&
            p.X >= Math.Min(a.X, b.X) - eps && p.X <= Math.Max(a.X, b.X) + eps &&
            p.Y >= Math.Min(a.Y, b.Y) - eps && p.Y <= Math.Max(a.Y, b.Y) + eps;
        }


        public static double Cross(Point2d a, Point2d b, Point2d c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);


        public static bool AabbOverlap(Extents2d a, Extents2d b)
        {
            return !(a.MaxPoint.X < b.MinPoint.X || a.MinPoint.X > b.MaxPoint.X ||
            a.MaxPoint.Y < b.MinPoint.Y || a.MinPoint.Y > b.MaxPoint.Y);
        }


        // Segment-segment intersection (inclusive of touching); returns intersection point when unique.
        public static bool SegmentIntersects(Line2d l, Segment2d s, double eps, out Point2d ip)
        {
            ip = default;


            // Quick reject: AABB
            if (!AabbOverlap(l.Bounds, s.Bounds)) return false;


            var o1 = Orient(l.A, l.B, s.A, eps);
            var o2 = Orient(l.A, l.B, s.B, eps);
            var o3 = Orient(s.A, s.B, l.A, eps);
            var o4 = Orient(s.A, s.B, l.B, eps);


            // General case
            if (o1 != o2 && o3 != o4)
            {
                // Compute intersection point (non-parallel) using line-line parametric form
                ip = LineLineIntersection(l.A, l.B, s.A, s.B);
                return true;
            }


            // Colinear or touching cases
            if (o1 == 0 && OnSegment(l.A, l.B, s.A, eps)) { ip = s.A; return true; }
            if (o2 == 0 && OnSegment(l.A, l.B, s.B, eps)) { ip = s.B; return true; }
            if (o3 == 0 && OnSegment(s.A, s.B, l.A, eps)) { ip = l.A; return true; }
            if (o4 == 0 && OnSegment(s.A, s.B, l.B, eps)) { ip = l.B; return true; }


            return false;
        }


        private static Point2d LineLineIntersection(Point2d p1, Point2d p2, Point2d p3, Point2d p4)
        {
            double x1 = p1.X, y1 = p1.Y, x2 = p2.X, y2 = p2.Y;
            double x3 = p3.X, y3 = p3.Y, x4 = p4.X, y4 = p4.Y;


            double den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(den) < 1e-20) return p2; // parallel/colinear fallback


            double px = ((x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4)) / den;
            double py = ((x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4)) / den;
            return new Point2d(px, py);
        }
    }
}
