using Autodesk.AutoCAD.Geometry;

using System;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{

    internal static class PolylineSegmentExtensions
    {
        private const double AngleTol = 1e-8; // ~5e-4 deg

        /// <summary>True when two consecutive segments meet with tangency.</summary>
        internal static bool IsTangentialTo(
            this IPolylineSegment first,
            IPolylineSegment second)
        {
            Vector2d t1 = GetEndTangentVector(first, atStart: false);
            Vector2d t2 = GetEndTangentVector(second, atStart: true);

            if (t1.IsZeroLength() || t2.IsZeroLength()) return true; // degenerate ⇒ treat as tangent

            return t1.GetAngleTo(t2) < AngleTol;
        }

        private static Vector2d GetEndTangentVector(IPolylineSegment seg, bool atStart)
        {
            switch (seg)
            {
                case PolylineLineSegment ln:
                    {
                        var g = (LineSegment2d)ln.GetGeometry2d();
                        return (g.EndPoint - g.StartPoint).GetNormal();
                    }

                case PolylineArcSegment ar:
                    {
                        var a = (CircularArc2d)ar.GetGeometry2d();
                        Point2d p = atStart ? a.StartPoint : a.EndPoint;
                        try
                        {
                            var tangent = a.GetTangent(p);
                            return tangent.Direction.GetNormal();
                        }
                        catch (Exception)
                        {
                            prdDbg($"c:{a.Center} sa:{a.StartAngle} ea:{a.EndAngle}");
                            throw;
                        }                        
                    }

                default:
                    throw new NotSupportedException("Unsupported segment type.");
            }
        }
    }
}