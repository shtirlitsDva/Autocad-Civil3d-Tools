namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Detailed failure reasons for debugging and error handling
    /// </summary>
    public enum FilletFailureReason
    {
        None,
        Seg1TooShort,
        Seg2TooShort,
        BothSegsTooShort,
        RadiusTooLarge,
        SegmentsDoNotIntersect,
        SegmentsAreTangential,
        UnsupportedSegmentTypes,
        CalculationError,
        InvalidRadius
    }
}
