using System.Collections.Generic;

namespace DimensioneringV2.Models.Report;

/// <summary>
/// HN-specific report settings that are saved with the HydraulicNetwork.
/// Contains metadata for the cover page and other per-calculation report data.
/// </summary>
internal class ReportHnSettings
{
    public string? ProjectName { get; set; }
    public string? ProjectNumber { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Author { get; set; }
    public string? Reviewer { get; set; }
    public string? Approver { get; set; }
    public string? CoverNote { get; set; }

    /// <summary>
    /// Design pressure in bar. Computed from critical path if not overridden.
    /// </summary>
    public double? DesignPressureBar { get; set; }

    /// <summary>
    /// Version history entries for the report.
    /// </summary>
    public List<VersionHistoryEntry> VersionHistory { get; set; } = new();
}

internal class VersionHistoryEntry
{
    public string? Revision { get; set; }
    public string? Date { get; set; }
    public string? Description { get; set; }
    public string? PerformedBy { get; set; }
    public string? ReviewedBy { get; set; }
}
