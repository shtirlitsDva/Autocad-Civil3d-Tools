using System.IO;

namespace SheetCreationAutomation.Models
{
    internal sealed class WizardRunOptions
    {
        public bool IsFirstFile { get; init; }
        public bool PlanOnly { get; init; }
        public string TemplateFilePath { get; init; } = string.Empty;
        public string TemplatePath => Path.GetFullPath(TemplateFilePath);
        public string TemplateFile => Path.GetFileName(TemplateFilePath);
        public int NextViewFrameCounterNumber { get; init; }
        public int ViewOverlap { get; init; }
    }
}
