namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Result of a fillet operation with detailed error reporting
    /// </summary>
    public class FilletResult
    {
        public bool Success { get; set; }
        public IPolylineSegment? TrimmedSegment1 { get; set; }
        public IPolylineSegment? FilletSegment { get; set; }
        public IPolylineSegment? TrimmedSegment2 { get; set; }
        public FilletFailureReason FailureReason { get; set; }
        public string? ErrorMessage { get; set; }

        public FilletResult(bool success = false)
        {
            Success = success;
            FailureReason = FilletFailureReason.None;
        }
    }
    #endregion
}
