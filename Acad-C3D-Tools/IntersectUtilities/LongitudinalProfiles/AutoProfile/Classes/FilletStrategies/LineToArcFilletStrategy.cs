using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal sealed class LineToArcFilletStrategy : IFilletStrategy
    {
        public bool CanHandle(IPolylineSegment s1, IPolylineSegment s2) =>
            s1 is PolylineLineSegment && s2 is PolylineArcSegment;

        public FilletResult CreateFillet(IPolylineSegment s1, IPolylineSegment s2, double r)
        {
            if (r <= 0) return new FilletResult(false) { FailureReason = FilletFailureReason.InvalidRadius };

            try
            {
                var ln = (LineSegment2d)((PolylineLineSegment)s1).GetGeometry2d();
                var arc = (CircularArc2d)((PolylineArcSegment)s2).GetGeometry2d();
                Point2d v = ln.EndPoint;                           // shared vertex

                Vector2d d1 = (ln.StartPoint - ln.EndPoint).GetNormal();
                Vector2d rad = v - arc.Center;
                Vector2d d2 = arc.IsClockWise
                             ? new Vector2d(rad.Y, -rad.X).GetNormal()
                             : new Vector2d(-rad.Y, rad.X).GetNormal();

                if (!FilletMath.TryConstructFillet(v, d1, d2, r, out Point2d t1, out Point2d t2,
                                                   out CircularArc2d f))
                    return new FilletResult(false) { FailureReason = FilletFailureReason.CalculationError };

                var trimmedLn = new LineSegment2d(ln.StartPoint, t1);
                double ns = Math.Atan2(t2.Y - arc.Center.Y, t2.X - arc.Center.X);
                var trimmedArc = new CircularArc2d(arc.Center, arc.Radius,
                                                   ns, arc.EndAngle,
                                                   Vector2d.XAxis,
                                                   arc.IsClockWise);

                return new FilletResult(true)
                {
                    TrimmedSegment1 = new PolylineLineSegment(trimmedLn),
                    FilletSegment = new PolylineArcSegment(f),
                    TrimmedSegment2 = new PolylineArcSegment(trimmedArc)
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
