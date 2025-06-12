using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Strategy for filleting two line segments
    /// </summary>
    internal sealed class LineToLineFilletStrategy : IFilletStrategy
    {
        public bool CanHandle(IPolylineSegment s1, IPolylineSegment s2) =>
            s1 is PolylineLineSegment && s2 is PolylineLineSegment;

        public FilletResult CreateFillet(IPolylineSegment s1, IPolylineSegment s2, double r)
        {
            if (r <= 0)
                return new FilletResult(false) { FailureReason = FilletFailureReason.InvalidRadius };

            try
            {
                var l1 = (LineSegment2d)((PolylineLineSegment)s1).GetGeometry2d();
                var l2 = (LineSegment2d)((PolylineLineSegment)s2).GetGeometry2d();

                // common vertex and outward directions
                Point2d v = l1.EndPoint;
                Vector2d d1 = (l1.StartPoint - l1.EndPoint).GetNormal();
                Vector2d d2 = (l2.EndPoint - l2.StartPoint).GetNormal();

                if (!FilletMath.TryConstructFillet(v, d1, d2, r,
                                                   out Point2d t1,
                                                   out Point2d t2,
                                                   out CircularArc2d fil))
                    return new FilletResult(false) { FailureReason = FilletFailureReason.SegmentsAreTangential };

                var trimmedL1 = new LineSegment2d(l1.StartPoint, t1);
                var trimmedL2 = new LineSegment2d(t2, l2.EndPoint);

                return new FilletResult(true)
                {
                    TrimmedSegment1 = new PolylineLineSegment(trimmedL1),
                    FilletSegment = new PolylineArcSegment(fil),
                    TrimmedSegment2 = new PolylineLineSegment(trimmedL2)
                };
            }
            catch (Exception ex)
            {
                return new FilletResult(false)
                {
                    FailureReason = FilletFailureReason.CalculationError,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}