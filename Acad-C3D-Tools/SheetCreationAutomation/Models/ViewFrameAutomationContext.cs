using System.Collections.Generic;

namespace SheetCreationAutomation.Models
{
    internal sealed class ViewFrameAutomationContext
    {
        public string ViewFrameFolder { get; init; } = string.Empty;
        public string FileListPath { get; init; } = string.Empty;
        public string TemplateFilePath { get; init; } = string.Empty;
        public int NextViewFrameCounterNumber { get; set; }
        public bool PlanOnly { get; init; }
        public int ViewOverlap { get; init; }
        public IReadOnlyList<string> DrawingPaths { get; init; } = new List<string>();
    }
}
