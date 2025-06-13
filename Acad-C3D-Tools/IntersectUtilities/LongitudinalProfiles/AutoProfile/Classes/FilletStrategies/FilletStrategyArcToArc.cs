using Autodesk.AutoCAD.Geometry;

using System;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile.Classes.FilletStrategies
{
    internal sealed class FilletStrategyArcToArc : IFilletStrategy
    {
        public bool CanHandle(IPolylineSegment s1, IPolylineSegment s2) =>
            s1 is PolylineArcSegment && s2 is PolylineArcSegment;

        public IFilletResult CreateFillet(IPolylineSegment s1, IPolylineSegment s2, double r)
        {
            if (r <= 0) return new FilletResultThreePart(false) { FailureReason = FilletFailureReason.InvalidRadius };

            try
            {
                var a1 = (CircularArc2d)((PolylineArcSegment)s1).GetGeometry2d();
                var a2 = (CircularArc2d)((PolylineArcSegment)s2).GetGeometry2d();
                Point2d v = a1.EndPoint;

                Vector2d rad1 = v - a1.Center;
                Vector2d tOut1 = a1.IsClockWise
                               ? new Vector2d(rad1.Y, -rad1.X).GetNormal()
                               : new Vector2d(-rad1.Y, rad1.X).GetNormal();
                Vector2d d1 = -tOut1;

                Vector2d rad2 = v - a2.Center;
                Vector2d d2 = a2.IsClockWise
                               ? new Vector2d(rad2.Y, -rad2.X).GetNormal()
                               : new Vector2d(-rad2.Y, rad2.X).GetNormal();

                if (!FilletMath.TryConstructFillet(v, d1, d2, r, out Point2d t1, out Point2d t2,
                                                   out CircularArc2d f))
                    return new FilletResultThreePart(false) { FailureReason = FilletFailureReason.CalculationError };

                var legCheck = FilletValidation.CheckLegRoom(s1, t1, s2, t2);
                if (legCheck != FilletFailureReason.None)
                    return new FilletResultThreePart(false) { FailureReason = legCheck };

                double newEnd1 = Math.Atan2(t1.Y - a1.Center.Y, t1.X - a1.Center.X);
                var trimmedA1 = new CircularArc2d(a1.Center, a1.Radius,
                                                     a1.StartAngle, newEnd1,
                                                     Vector2d.XAxis,
                                                     a1.IsClockWise);

                double newStart2 = Math.Atan2(t2.Y - a2.Center.Y, t2.X - a2.Center.X);
                var trimmedA2 = new CircularArc2d(a2.Center, a2.Radius,
                                                     newStart2, a2.EndAngle,
                                                     Vector2d.XAxis,
                                                     a2.IsClockWise);

                return new FilletResultThreePart(true)
                {
                    TrimmedSegment1 = new PolylineArcSegment(trimmedA1),
                    FilletSegment = new PolylineArcSegment(f),
                    TrimmedSegment2 = new PolylineArcSegment(trimmedA2)
                };
            }
            catch (Exception ex)
            {
                return new FilletResultThreePart(false)
                { FailureReason = FilletFailureReason.CalculationError, ErrorMessage = ex.Message };
            }
        }
    }
}
