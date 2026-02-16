namespace SheetCreationAutomation.Models
{
    internal sealed class ViewFramesUiState
    {
        public string ViewFrameFolder { get; set; } = string.Empty;
        public string FileListPath { get; set; } = string.Empty;
        public string TemplateFileName { get; set; } = string.Empty;
        public string VfNumber { get; set; } = "1";
        public string ViewOverlap { get; set; } = "40";
        public bool PlanOnly { get; set; }
    }
}
