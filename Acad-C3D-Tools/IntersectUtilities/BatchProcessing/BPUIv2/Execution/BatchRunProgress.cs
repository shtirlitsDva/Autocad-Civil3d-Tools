namespace IntersectUtilities.BatchProcessing.BPUIv2.Execution;

public class BatchRunProgress
{
    public int DrawingIndex { get; }
    public int TotalDrawings { get; }
    public string DrawingName { get; }
    public string OperationName { get; }
    public int OperationIndex { get; }
    public int TotalOperations { get; }

    /// <summary>
    /// Overall progress as a percentage (0-100).
    /// </summary>
    public int PercentComplete
    {
        get
        {
            if (TotalDrawings == 0) return 0;
            double drawingProgress = (double)DrawingIndex / TotalDrawings;
            double opProgress = TotalOperations > 0
                ? (double)OperationIndex / TotalOperations / TotalDrawings
                : 0;
            return (int)((drawingProgress + opProgress) * 100);
        }
    }

    public BatchRunProgress(
        int drawingIndex,
        int totalDrawings,
        string drawingName,
        string operationName,
        int operationIndex,
        int totalOperations)
    {
        DrawingIndex = drawingIndex;
        TotalDrawings = totalDrawings;
        DrawingName = drawingName;
        OperationName = operationName;
        OperationIndex = operationIndex;
        TotalOperations = totalOperations;
    }
}
