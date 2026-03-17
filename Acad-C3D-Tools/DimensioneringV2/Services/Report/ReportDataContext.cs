using DimensioneringV2.Models;
using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.DataModels;

using NorsynHydraulicCalc;

using System.Collections.Generic;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Read-only data snapshot consumed by all report modules.
/// Built once by ReportDataExtractor before rendering.
/// </summary>
internal class ReportDataContext
{
    public required HydraulicNetwork Network { get; init; }
    public required HydraulicSettings Settings { get; init; }
    public required ReportHnSettings HnSettings { get; init; }
    public required ReportProfile Profile { get; init; }

    public required SystemSummary Summary { get; init; }
    public required List<SegmentRow> Segments { get; init; }
    public required List<NodeRow> Nodes { get; init; }
    public required List<ConsumerRow> Consumers { get; init; }
    public required List<ComplianceRow> ComplianceChecks { get; init; }
    public required List<SupplyPointRow> SupplyPoints { get; init; }

    /// <summary>
    /// Current top-level section number, set by the orchestrator before each module.
    /// Modules use this for dynamic section headings (e.g. "{CurrentSection}.1 Sub-title").
    /// </summary>
    public int CurrentSection { get; set; }
}
