namespace SheetCreationAutomation.Models
{
    internal sealed class AhkViewFrameSettings
    {
        public string? viewFramesFolderPath { get; set; }
        public string? templateFileName { get; set; }
        public string? nextViewFrameCounterNumber { get; set; }
        public string? fileListFileName { get; set; }
        public bool? planOnly { get; set; }
        public string? viewOverlap { get; set; }
    }
}
