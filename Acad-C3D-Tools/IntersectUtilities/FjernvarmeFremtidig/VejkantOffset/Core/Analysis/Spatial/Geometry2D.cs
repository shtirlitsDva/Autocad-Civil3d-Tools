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
        public static int Orient(Point2d a, Point2d b, Point2d c, double eps)
        {
            double v = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (v > eps) return 1;
            if (v < -eps) return -1;
            return 0;
        }
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

        public static List<Point2d> BuildBufferRectangle(Point2d a, Point2d b, double offset)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-20) return new List<Point2d> { a };

            double ux = dx / len;
            double uy = dy / len;
            // perpendicular
            double px = -uy * offset;
            double py = ux * offset;

            var p1 = new Point2d(a.X + px, a.Y + py);
            var p2 = new Point2d(b.X + px, b.Y + py);
            var p3 = new Point2d(b.X - px, b.Y - py);
            var p4 = new Point2d(a.X - px, a.Y - py);

            return new List<Point2d> { p1, p2, p3, p4 };
        }

        public static Extents2d BoundsOfPolygon(List<Point2d> pts)
        {
            double minX = pts.Min(p => p.X);
            double minY = pts.Min(p => p.Y);
            double maxX = pts.Max(p => p.X);
            double maxY = pts.Max(p => p.Y);
            return new Extents2d(new Point2d(minX, minY), new Point2d(maxX, maxY));
        }

        // Check if segment intersects or is inside polygon (convex quad in this case)
        public static bool SegmentIntersectsPolygon(Segment2d seg, List<Point2d> poly, double eps)
        {
            // Check if either endpoint is inside polygon
            if (PointInPolygon(seg.A, poly) || PointInPolygon(seg.B, poly)) return true;
            // Check segment against polygon edges
            for (int i = 0; i < poly.Count; i++)
            {
                var p1 = poly[i];
                var p2 = poly[(i + 1) % poly.Count];
                var l = new Line2d(p1, p2);
                if (SegmentIntersects(l, seg, eps, out _)) return true;
            }
            return false;
        }

        private static bool PointInPolygon(Point2d pt, List<Point2d> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].Y > pt.Y) != (poly[j].Y > pt.Y)) &&
                    (pt.X < (poly[j].X - poly[i].X) * (pt.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static OrientedRect MakeLineBuffer(Point2d a, Point2d b, double halfWidth)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-20) len = 1e-20; // avoid div by zero
            double ux = dx / len, uy = dy / len;        // x-axis along line
            double vx = -uy, vy = ux;                   // y-axis (left normal)
            return new OrientedRect(a, ux, uy, vx, vy, len, halfWidth);
        }

        public static Extents2d OrientedRectAabb(in OrientedRect r)
        {
            var c0 = r.Corner00; var c1 = r.Corner01; var c2 = r.Corner11; var c3 = r.Corner10;
            double minX = Math.Min(Math.Min(c0.X, c1.X), Math.Min(c2.X, c3.X));
            double minY = Math.Min(Math.Min(c0.Y, c1.Y), Math.Min(c2.Y, c3.Y));
            double maxX = Math.Max(Math.Max(c0.X, c1.X), Math.Max(c2.X, c3.X));
            double maxY = Math.Max(Math.Max(c0.Y, c1.Y), Math.Max(c2.Y, c3.Y));
            return new Extents2d(new Point2d(minX, minY), new Point2d(maxX, maxY));
        }

        // Transform a world point into the oriented-rect's local coordinates
        public static (double x, double y) ToLocal(in OrientedRect r, in Point2d p)
        {
            // solve [Ux Vx; Uy Vy] * [x;y] = p - origin
            double px = p.X - r.Origin.X;
            double py = p.Y - r.Origin.Y;
            double det = r.Ux * r.Vy - r.Uy * r.Vx;
            if (Math.Abs(det) < 1e-20) det = 1e-20;
            double inv00 = r.Vy / det;
            double inv01 = -r.Vx / det;
            double inv10 = -r.Uy / det;
            double inv11 = r.Ux / det;
            double x = inv00 * px + inv01 * py;
            double y = inv10 * px + inv11 * py;
            return (x, y);
        }

        // Liang–Barsky clipping of a segment against axis-aligned box [0,L] x [-w,w] in local coords
        public static bool SegmentOverlapsOrientedRect(in Segment2d seg, in OrientedRect r)
        {
            var (x0, y0) = ToLocal(r, seg.A);
            var (x1, y1) = ToLocal(r, seg.B);

            double dx = x1 - x0, dy = y1 - y0;
            double t0 = 0.0, t1 = 1.0;

            bool Clip(double p, double q)
            {
                if (Math.Abs(p) < 1e-20) return q >= 0; // parallel to this boundary
                double t = q / p;
                if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
                return true;
            }

            // x >= 0       =>  +dx * t + (x0) >= 0     =>  p=+dx, q= -x0
            if (!Clip(+dx, -x0)) return false;
            // x <= L       =>  -dx * t + (L - x0) >= 0 =>  p=-dx, q= r.Length - x0
            if (!Clip(-dx, r.Length - x0)) return false;
            // y >= -w      =>  +dy * t + (y0 + w) >= 0 =>  p=+dy, q= y0 + r.HalfWidth
            if (!Clip(+dy, y0 + r.HalfWidth)) return false;
            // y <= +w      =>  -dy * t + (w - y0) >= 0 =>  p=-dy, q= r.HalfWidth - y0
            if (!Clip(-dy, r.HalfWidth - y0)) return false;

            return t0 <= t1;
        }
    }
}
