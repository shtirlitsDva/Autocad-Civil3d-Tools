namespace DimensioneringV2.Models.Report;

internal class ReportModuleEntry
{
    public ReportModuleId ModuleId { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }

    public ReportModuleEntry() { }

    public ReportModuleEntry(ReportModuleId moduleId, bool isEnabled, int sortOrder)
    {
        ModuleId = moduleId;
        IsEnabled = isEnabled;
        SortOrder = sortOrder;
    }
}
