using Autodesk.AutoCAD.Geometry;

using System;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal sealed class FilletStrategyArcToLine : IFilletStrategy
    {
        public bool CanHandle(IPolylineSegment s1, IPolylineSegment s2) =>
            s1 is PolylineArcSegment && s2 is PolylineLineSegment;

        public IFilletResult CreateFillet(IPolylineSegment s1, IPolylineSegment s2, double r)
        {
            if (r <= 0)
                return new FilletResultThreePart(false)
                { FailureReason = FilletFailureReason.InvalidRadius };

            try
            {
                var arc = (CircularArc2d)((PolylineArcSegment)s1).GetGeometry2d();
                var ln = (LineSegment2d)((PolylineLineSegment)s2).GetGeometry2d();

                Point2d v = arc.EndPoint;                       // join vertex
                Vector2d uL = (ln.EndPoint - ln.StartPoint).GetNormal();

                // ---- 1.  analytic centre & tangent points --------------------------
                if (!FilletMath.TryLineArcTangent(
                        ln: ln, arc: arc,                        
                        filletR: r,
                        out Point2d cen, out Point2d pArc, out Point2d pLin))
                    return new FilletResultThreePart(false)
                    { FailureReason = FilletFailureReason.RadiusTooLarge };

                var legCheck = FilletValidation.CheckLegRoom(s1, pArc, s2, pLin);
                if (legCheck != FilletFailureReason.None)
                    return new FilletResultThreePart(false) { FailureReason = legCheck };

                // ---- 2.  trim arc & line exactly at those points -------------------
                var trimmedArc = FilletMath.TrimArcToPoint(arc, pArc, trimEnd: true);
                var trimmedLn = new LineSegment2d(pLin, ln.EndPoint);

                // ---- 3.  build fillet arc ------------------------------------------
                bool cw = ((pArc - cen).X * (pLin - cen).Y -
                           (pArc - cen).Y * (pLin - cen).X) < 0.0;

                double aStartCCW = Math.Atan2(pArc.Y - cen.Y, pArc.X - cen.X);
                double aEndCCW = Math.Atan2(pLin.Y - cen.Y, pLin.X - cen.X);

                double startAng, endAng;
                if (cw)
                {
                    startAng = 2 * Math.PI - aStartCCW;
                    endAng = 2 * Math.PI - aEndCCW;
                    if (endAng <= startAng) endAng += 2 * Math.PI;
                }
                else
                {
                    startAng = aStartCCW;
                    endAng = aEndCCW;
                    if (endAng <= startAng) endAng += 2 * Math.PI;
                }

                var fillet = new CircularArc2d(cen, r,
                                               startAng, endAng,
                                               Vector2d.XAxis, cw);

                return new FilletResultThreePart(true)
                {
                    TrimmedSegment1 = new PolylineArcSegment(trimmedArc),
                    FilletSegment = new PolylineArcSegment(fillet),
                    TrimmedSegment2 = new PolylineLineSegment(trimmedLn)
                };
            }
            catch (Exception ex)
            {
                return new FilletResultThreePart(false)
                {
                    FailureReason = FilletFailureReason.CalculationError,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
