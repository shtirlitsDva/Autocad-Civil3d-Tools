using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial
{
    internal readonly struct Segment2d
    {
        public Point2d A { get; }
        public Point2d B { get; }
        public ObjectId PolylineId { get; }
        public Extents2d Bounds { get; }

        public Segment2d(Point2d a, Point2d b, ObjectId id)
        {
            A = a;
            B = b;
            PolylineId = id;
            Bounds = new Extents2d(
            new Point2d(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)),
            new Point2d(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y))
            );
        }

        public static bool Intersects(Point2d p1, Point2d p2, Point2d q1, Point2d q2, double tol = 1e-9)
        {
            // Orientation test helper
            static double Orient(Point2d a, Point2d b, Point2d c)
            {
                return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            }

            double o1 = Orient(p1, p2, q1);
            double o2 = Orient(p1, p2, q2);
            double o3 = Orient(q1, q2, p1);
            double o4 = Orient(q1, q2, p2);

            if ((o1 * o2 < -tol) && (o3 * o4 < -tol))
                return true; // proper intersection

            // Handle collinear special cases
            if (Math.Abs(o1) < tol && OnSegment(p1, p2, q1)) return true;
            if (Math.Abs(o2) < tol && OnSegment(p1, p2, q2)) return true;
            if (Math.Abs(o3) < tol && OnSegment(q1, q2, p1)) return true;
            if (Math.Abs(o4) < tol && OnSegment(q1, q2, p2)) return true;

            return false;
        }

        private static bool OnSegment(Point2d a, Point2d b, Point2d c)
        {
            return c.X <= Math.Max(a.X, b.X) && c.X >= Math.Min(a.X, b.X)
            && c.Y <= Math.Max(a.Y, b.Y) && c.Y >= Math.Min(a.Y, b.Y);
        }
    }
}
