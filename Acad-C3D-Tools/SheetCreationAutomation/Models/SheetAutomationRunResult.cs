namespace SheetCreationAutomation.Models
{
    internal sealed class SheetAutomationRunResult
    {
        public bool Succeeded { get; init; }
        public AutomationFailureInfo? Failure { get; init; }
    }
}
