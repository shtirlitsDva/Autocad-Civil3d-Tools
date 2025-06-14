using Autodesk.AutoCAD.Geometry;

using System;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal sealed class FilletStrategyLineToArc : IFilletStrategy
    {
        public bool CanHandle(IPolylineSegment s1, IPolylineSegment s2) =>
            s1 is PolylineLineSegment && s2 is PolylineArcSegment;

        public IFilletResult CreateFillet(IPolylineSegment s1, IPolylineSegment s2, double r)
        {
            if (r <= 0)
                return new FilletResultThreePart()
                { FailureReason = FilletFailureReason.InvalidRadius };

            try
            {
                var ln = (LineSegment2d)((PolylineLineSegment)s1).GetGeometry2d();
                var arc = (CircularArc2d)((PolylineArcSegment)s2).GetGeometry2d();

                Point2d v = ln.EndPoint;
                Vector2d uL = (ln.EndPoint - ln.StartPoint).GetNormal();

                // ---- 1.  analytic solution -----------------------------------------
                if (!FilletMath.TryLineArcTangent(
                        ln: ln, arc: arc,
                        filletR: r,
                        out Point2d cen, out Point2d pArc, out Point2d pLin))
                    return new FilletResultThreePart()
                    { FailureReason = FilletFailureReason.RadiusTooLarge };

                var legCheck = FilletValidation.CheckLegRoom(s1, pLin, s2, pArc);
                if (legCheck != FilletFailureReason.None)
                    return new FilletResultThreePart() { FailureReason = legCheck };

#if DEBUG
                //var dbg = FilletMath.DumpLineArcDebug(ln, arc, v, r, cen, pLin, pArc, legCheck);
                //prdDbg(dbg);
                //throw new InvalidOperationException();
#endif

                // ---- 2.  trim primitives -------------------------------------------
                var trimmedLn = new LineSegment2d(ln.StartPoint, pLin);
                var trimmedArc = FilletMath.TrimArcToPoint(arc, pArc, trimEnd: false);

                // ---- 3.  fillet arc -------------------------------------------------
                bool cw = ((pLin - cen).X * (pArc - cen).Y -
                           (pLin - cen).Y * (pArc - cen).X) < 0.0;

                double aStartCCW = Math.Atan2(pLin.Y - cen.Y, pLin.X - cen.X);
                double aEndCCW = Math.Atan2(pArc.Y - cen.Y, pArc.X - cen.X);

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

                return new FilletResultThreePart(
                    originalFirstSegment: s1,
                    originalSecondSegment: s2,
                    trimmedSegment1: new PolylineLineSegment(trimmedLn),
                    filletSegment: new PolylineArcSegment(fillet),
                    trimmedSegment2: new PolylineArcSegment(trimmedArc));

            }
            catch (Exception ex)
            {
                return new FilletResultThreePart()
                {
                    FailureReason = FilletFailureReason.CalculationError,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
