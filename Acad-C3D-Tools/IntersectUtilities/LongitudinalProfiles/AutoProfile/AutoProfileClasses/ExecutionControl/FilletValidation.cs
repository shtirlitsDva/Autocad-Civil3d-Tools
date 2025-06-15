using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal static class FilletValidation
    {
        /// <summary>
        /// Checks whether the tangent points fall inside the available legs.
        /// </summary>
        internal static FilletFailureReason CheckLegRoom(
            IPolylineSegment seg1, Point2d t1,
            IPolylineSegment seg2, Point2d t2)
        {
            bool ok1 = seg1.GetGeometry2d().IsOn(t1);
            bool ok2 = seg2.GetGeometry2d().IsOn(t2);

            if (ok1 && ok2) return FilletFailureReason.None;
            if (!ok1 && !ok2) return FilletFailureReason.BothSegsTooShort;
            return ok1 ? FilletFailureReason.Seg2TooShort
                        : FilletFailureReason.Seg1TooShort;
        }
    }
}