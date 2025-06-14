using Autodesk.AutoCAD.Geometry;
using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Windows.Documents;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal sealed class FilletStrategyArcToArc : IFilletStrategy
    {
        public bool CanHandle(IPolylineSegment s1, IPolylineSegment s2) =>
            s1 is PolylineArcSegment && s2 is PolylineArcSegment;

        public IFilletResult CreateFillet(IPolylineSegment s1, IPolylineSegment s2, double r)
        {
            if (r <= 0)
                return new FilletResultThreePart()
                { FailureReason = FilletFailureReason.InvalidRadius };

            try
            {
                var arc1 = (CircularArc2d)((PolylineArcSegment)s1).GetGeometry2d();
                var arc2 = (CircularArc2d)((PolylineArcSegment)s2).GetGeometry2d();

                // ---- 1.  centres that satisfy CiF = Ri + r --------------------------
                if (!FilletMath.TryCircleCircleTangent(
                        arc1.Center, arc1.Radius,
                        arc2.Center, arc2.Radius,
                        r,
                        out Point2d cA, out Point2d cB))
                    return new FilletResultThreePart()
                    { FailureReason = FilletFailureReason.RadiusTooLarge };

                // choose the centre that sits inside the interior angle
                Point2d ChooseValidCentre(Point2d candCen)
                {
                    Vector2d d1 = (candCen - arc1.Center).GetNormal();
                    Vector2d d2 = (candCen - arc2.Center).GetNormal();

                    Point2d q1 = arc1.Center + d1 * arc1.Radius;
                    Point2d q2 = arc2.Center + d2 * arc2.Radius;

                    return (arc1.IsOn(q1) && arc2.IsOn(q2)) ? candCen : Point2d.Origin;
                }

                Point2d filletCen = ChooseValidCentre(cA);
                if (filletCen == Point2d.Origin)
                    filletCen = ChooseValidCentre(cB);
                if (filletCen == Point2d.Origin)
                    return new FilletResultThreePart()
                    { FailureReason = FilletFailureReason.RadiusTooLarge };

                // ---- 2.  tangent points ------------------------------------------------
                Vector2d dir1 = (filletCen - arc1.Center).GetNormal();
                Vector2d dir2 = (filletCen - arc2.Center).GetNormal();

                Point2d p1 = arc1.Center + dir1 * arc1.Radius;
                Point2d p2 = arc2.Center + dir2 * arc2.Radius;

                // ---- 3.  trim source arcs exactly at those points ---------------------
                var trimmed1 = FilletMath.TrimArcToPoint(arc1, p1, trimEnd: true);
                var trimmed2 = FilletMath.TrimArcToPoint(arc2, p2, trimEnd: false);
#if DEBUG
                //var dbg = FilletMath.DumpArcToArcDebug(
                //    arc1, arc2, v, cA, cB, filletCen, dir1, dir2, p1, p2, trimmed1, trimmed2);
                //prdDbg(dbg);
                //throw new InvalidOperationException();
#endif

                // ---- 4.  build fillet arc --------------------------------------------
                bool cwFil = dir1.CrossProduct(dir2) < 0;
                double aStartCCW = Math.Atan2(p1.Y - filletCen.Y, p1.X - filletCen.X);
                double aEndCCW = Math.Atan2(p2.Y - filletCen.Y, p2.X - filletCen.X);

                double startAng, endAng;
                if (cwFil)
                {
                    // convert each CCW angle to its clockwise measure
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

                var fillet = new CircularArc2d(filletCen, r,
                                               startAng, endAng,
                                               Vector2d.XAxis, cwFil);

                return new FilletResultThreePart(
                    originalFirstSegment: s1,
                    originalSecondSegment: s2,
                    trimmedSegment1: new PolylineArcSegment(trimmed1),
                    filletSegment: new PolylineArcSegment(fillet),
                    trimmedSegment2: new PolylineArcSegment(trimmed2));
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