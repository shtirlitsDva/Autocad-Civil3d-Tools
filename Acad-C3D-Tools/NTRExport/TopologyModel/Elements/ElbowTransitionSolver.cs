using System;
using Autodesk.AutoCAD.Geometry;

namespace NTRExport.TopologyModel
{
    internal static class ElbowTransitionSolver
    {
        internal readonly record struct ElbowSolution(double PhiRad, double Cy)
        {
            public double PhiDeg => PhiRad * 180.0 / Math.PI;
        }

        /// <summary>
        /// Solve for φ in [0, π/2] given vertical drop H and elbow radius R.
        /// Uses: H = R * (sin φ + 1 - cos φ).
        /// Returns φ and Cy = R * (cos φ + sin φ).
        /// </summary>
        internal static ElbowSolution Solve(double h, double radius, double tolerance = 1e-12, int maxIter = 80)
        {
            if (h < 0.0)
                throw new ArgumentOutOfRangeException(nameof(h), "Vertical drop must be non-negative.");
            if (radius <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be positive.");

            double hMax = 2.0 * radius;
            if (h > hMax + 1e-9)
                throw new InvalidOperationException(
                    $"No geometric solution: drop {h:0.###} exceeds radius sum {hMax:0.###}.");

            double F(double phi) =>
                radius * (Math.Sin(phi) + 1.0 - Math.Cos(phi)) - h;

            double a = 0.0;
            double b = 0.5 * Math.PI;
            double fa = F(a);
            double fb = F(b);

            if (Math.Abs(fa) <= tolerance)
                return FromPhi(a, radius);
            if (Math.Abs(fb) <= tolerance)
                return FromPhi(b, radius);

            if (fa * fb > 0.0)
                throw new InvalidOperationException(
                    $"Could not bracket elbow solution on [0,π/2] for H={h:0.###}, R={radius:0.###}.");

            double phi = 0.0;
            for (int i = 0; i < maxIter; i++)
            {
                phi = 0.5 * (a + b);
                double fm = F(phi);
                if (Math.Abs(fm) <= tolerance)
                    break;

                if (fa * fm <= 0.0)
                {
                    b = phi;
                    fb = fm;
                }
                else
                {
                    a = phi;
                    fa = fm;
                }
            }

            return FromPhi(phi, radius);
        }

        internal static Point3d ComputeTangentIntersection(
            Point3d startPoint,
            Vector3d startDirection,
            Point3d endPoint,
            Vector3d endDirection,
            double radius)
        {
            if (radius <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be positive.");

            var dStart = Normalize(startDirection, nameof(startDirection));
            var dEnd = Normalize(endDirection, nameof(endDirection));

            var tangentOrigin1 = startPoint + dStart.MultiplyBy(radius);
            var tangentOrigin2 = endPoint - dEnd.MultiplyBy(radius);
            var dir1 = dStart;
            var dir2 = dEnd.Negate();

            var intersection = IntersectLines(tangentOrigin1, dir1, tangentOrigin2, dir2);
            if (intersection.HasValue)
                return intersection.Value;

            return new Point3d(
                0.5 * (tangentOrigin1.X + tangentOrigin2.X),
                0.5 * (tangentOrigin1.Y + tangentOrigin2.Y),
                0.5 * (tangentOrigin1.Z + tangentOrigin2.Z));
        }

        private static ElbowSolution FromPhi(double phi, double radius)
        {
            double cy = radius * (Math.Cos(phi) + Math.Sin(phi));
            return new ElbowSolution(phi, cy);
        }

        private static Vector3d Normalize(Vector3d vector, string paramName)
        {
            if (vector.Length < 1e-9)
                throw new ArgumentOutOfRangeException(paramName, "Direction vector must be non-zero.");
            return vector.GetNormal();
        }

        private static Point3d? IntersectLines(
            Point3d p1,
            Vector3d d1,
            Point3d p2,
            Vector3d d2)
        {
            const double tol = 1e-9;
            var w0 = p1 - p2;
            var a = d1.DotProduct(d1);
            var b = d1.DotProduct(d2);
            var c = d2.DotProduct(d2);
            var d = d1.DotProduct(w0);
            var e = d2.DotProduct(w0);
            var denom = a * c - b * b;

            if (Math.Abs(denom) < tol)
                return null;

            var sc = (b * e - c * d) / denom;
            var tc = (a * e - b * d) / denom;

            var pointOnFirst = p1 + d1.MultiplyBy(sc);
            var pointOnSecond = p2 + d2.MultiplyBy(tc);

            return new Point3d(
                0.5 * (pointOnFirst.X + pointOnSecond.X),
                0.5 * (pointOnFirst.Y + pointOnSecond.Y),
                0.5 * (pointOnFirst.Z + pointOnSecond.Z));
        }
    }
}

