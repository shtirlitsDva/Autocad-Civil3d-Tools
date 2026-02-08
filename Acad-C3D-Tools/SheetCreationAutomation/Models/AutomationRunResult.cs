namespace SheetCreationAutomation.Models
{
    internal sealed class AutomationRunResult
    {
        public bool Succeeded { get; init; }
        public int FinalNextViewFrameCounter { get; init; }
        public AutomationFailureInfo? Failure { get; init; }
    }
}
