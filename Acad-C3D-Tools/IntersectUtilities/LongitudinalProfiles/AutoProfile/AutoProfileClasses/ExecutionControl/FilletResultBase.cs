using System.Collections.Generic;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Result of a fillet operation with detailed error reporting
    /// </summary>
    public abstract class FilletResultBase : IFilletResult
    {
        public bool Success { get; set; }
        public FilletFailureReason FailureReason { get; set; }
        public string? ErrorMessage { get; set; }

        public FilletResultBase(bool success = false)
        {
            Success = success;
            FailureReason = FilletFailureReason.None;
        }

        public abstract void UpdateWithResults(
            LinkedList<IPolylineSegment> segments);
    }
}
