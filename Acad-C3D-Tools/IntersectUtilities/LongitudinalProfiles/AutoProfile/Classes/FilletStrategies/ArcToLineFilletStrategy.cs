using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal sealed class ArcToLineFilletStrategy : IFilletStrategy
    {
        public bool CanHandle(IPolylineSegment s1, IPolylineSegment s2) =>
            s1 is PolylineArcSegment && s2 is PolylineLineSegment;

        public FilletResult CreateFillet(IPolylineSegment s1, IPolylineSegment s2, double r)
        {
            if (r <= 0) return new FilletResult(false) { FailureReason = FilletFailureReason.InvalidRadius };

            try
            {
                var arc = (CircularArc2d)((PolylineArcSegment)s1).GetGeometry2d();
                var ln = (LineSegment2d)((PolylineLineSegment)s2).GetGeometry2d();
                Point2d v = arc.EndPoint;                            // shared vertex

                Vector2d rad = v - arc.Center;
                Vector2d tOut = arc.IsClockWise
                              ? new Vector2d(rad.Y, -rad.X).GetNormal()
                              : new Vector2d(-rad.Y, rad.X).GetNormal();
                Vector2d d1 = -tOut;                                 // into arc interior
                Vector2d d2 = (ln.EndPoint - ln.StartPoint).GetNormal();

                if (!FilletMath.TryConstructFillet(v, d1, d2, r, out Point2d t1, out Point2d t2,
                                                   out CircularArc2d f))
                    return new FilletResult(false) { FailureReason = FilletFailureReason.CalculationError };

                double newEnd = Math.Atan2(t1.Y - arc.Center.Y, t1.X - arc.Center.X);
                var trimmedArc = new CircularArc2d(arc.Center, arc.Radius,
                                                   arc.StartAngle, newEnd,
                                                    Vector2d.XAxis,
                                                   arc.IsClockWise);
                var trimmedLn = new LineSegment2d(t2, ln.EndPoint);

                return new FilletResult(true)
                {
                    TrimmedSegment1 = new PolylineArcSegment(trimmedArc),
                    FilletSegment = new PolylineArcSegment(f),
                    TrimmedSegment2 = new PolylineLineSegment(trimmedLn)
                };
            }
            catch (Exception ex)
            {
                return new FilletResult(false)
                { FailureReason = FilletFailureReason.CalculationError, ErrorMessage = ex.Message };
            }
        }
    }
}
