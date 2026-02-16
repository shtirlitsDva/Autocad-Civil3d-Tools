namespace SheetCreationAutomation.Models
{
    internal sealed class CreateSheetsWizardRunOptions
    {
        public bool PlanOnly { get; init; }
        public string SheetSetFilePath { get; init; } = string.Empty;
        public string LayoutNamePattern { get; init; } = string.Empty;
        public string SheetFileNamePattern { get; init; } = string.Empty;
        public string NorthArrowBlockName { get; init; } = string.Empty;
    }
}
