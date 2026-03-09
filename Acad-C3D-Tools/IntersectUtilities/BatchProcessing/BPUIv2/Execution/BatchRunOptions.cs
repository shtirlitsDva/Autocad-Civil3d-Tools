namespace IntersectUtilities.BatchProcessing.BPUIv2.Execution;

public class BatchRunOptions
{
    /// <summary>
    /// If true, skip remaining operations for a drawing when a FatalError occurs.
    /// The drawing is NOT saved.
    /// </summary>
    public bool AbortDrawingOnFatal { get; set; } = true;

    /// <summary>
    /// If true, stop processing all remaining drawings when any exception occurs.
    /// </summary>
    public bool AbortAllOnException { get; set; } = false;
}
