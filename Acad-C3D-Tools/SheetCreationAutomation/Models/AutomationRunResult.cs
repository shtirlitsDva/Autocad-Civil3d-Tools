namespace SheetCreationAutomation.Models
{
    internal sealed class AutomationRunResult
    {
        public AutomationOutcome Outcome { get; init; }
        public int FinalNextViewFrameCounter { get; init; }
        public AutomationFailureInfo? Failure { get; init; }
    }
}
