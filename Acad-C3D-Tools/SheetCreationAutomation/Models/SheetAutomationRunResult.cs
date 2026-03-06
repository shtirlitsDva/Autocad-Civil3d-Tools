namespace SheetCreationAutomation.Models
{
    internal sealed class SheetAutomationRunResult
    {
        public AutomationOutcome Outcome { get; init; }
        public AutomationFailureInfo? Failure { get; init; }
    }
}
