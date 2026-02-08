namespace SheetCreationAutomation.Models
{
    internal sealed class WizardRunOptions
    {
        public bool IsFirstFile { get; init; }
        public bool PlanOnly { get; init; }
        public string TemplateFileName { get; init; } = string.Empty;
        public int NextViewFrameCounterNumber { get; init; }
        public int ViewOverlap { get; init; }
    }
}
