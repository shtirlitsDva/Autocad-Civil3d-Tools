using System.Collections.Generic;

namespace SheetCreationAutomation.Models
{
    internal sealed class SheetAutomationContext
    {
        public string ViewFrameFolder { get; init; } = string.Empty;
        public string FileListPath { get; init; } = string.Empty;
        public string SheetSetFilePath { get; init; } = string.Empty;
        public string ProfileViewOrigin { get; init; } = string.Empty;
        public bool PlanOnly { get; init; }
        public IReadOnlyList<string> DrawingPaths { get; init; } = new List<string>();
    }
}
