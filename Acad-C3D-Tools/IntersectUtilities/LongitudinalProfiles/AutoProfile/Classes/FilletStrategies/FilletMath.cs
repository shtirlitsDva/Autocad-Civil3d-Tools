using Autodesk.AutoCAD.Geometry;

using System;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal static class FilletMath
    {
        private const double Eps = 1e-9;

        internal static bool TryConstructFillet(
            Point2d v,           // common vertex
            Vector2d d1,          // unit dir away from v along seg‑1
            Vector2d d2,          // unit dir away from v along seg‑2
            double r,
            out Point2d p1,        // tangent on seg‑1
            out Point2d p2,        // tangent on seg‑2
            out CircularArc2d fil) // resulting fillet arc
        {
            p1 = p2 = default;
            fil = null;

            double ang = d1.GetAngleTo(d2);
            if (ang < Eps || ang > Math.PI - Eps) return false;

            double t = r / Math.Tan(ang / 2.0);
            p1 = v + d1 * t;
            p2 = v + d2 * t;

            Vector2d bis = (d1 + d2).GetNormal();
            double off = r / Math.Sin(ang / 2.0);
            Point2d cen = v + bis * off;

            static double NormCCW(double a)
            {
                a %= 2 * Math.PI;
                return a < 0 ? a + 2 * Math.PI : a;
            }

            double a1CCW = NormCCW(Math.Atan2(p1.Y - cen.Y, p1.X - cen.X));
            double a2CCW = NormCCW(Math.Atan2(p2.Y - cen.Y, p2.X - cen.X));

            bool cw = ((p1 - cen).X * (p2 - cen).Y -
                       (p1 - cen).Y * (p2 - cen).X) < 0.0;

            double startAng, endAng;
            if (cw)
            {
                // convert to clockwise measurement
                startAng = 2 * Math.PI - a1CCW;
                endAng = 2 * Math.PI - a2CCW;
            }
            else
            {
                startAng = a1CCW;
                endAng = a2CCW;
            }

            // enforce end > start as required by AcGe
            if (endAng <= startAng) endAng += 2 * Math.PI;

            fil = new CircularArc2d(cen, r, startAng, endAng, Vector2d.XAxis, cw);
            return true;
        }
    }
}
