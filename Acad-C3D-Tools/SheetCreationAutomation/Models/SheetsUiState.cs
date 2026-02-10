namespace SheetCreationAutomation.Models
{
    internal sealed class SheetsUiState
    {
        public bool PlanOnly { get; set; }
        public string ViewFrameFolder { get; set; } = string.Empty;
        public string FileListPath { get; set; } = string.Empty;
        public string SheetSetLocation { get; set; } = string.Empty;
        public string Coordinates { get; set; } = string.Empty;
    }
}
