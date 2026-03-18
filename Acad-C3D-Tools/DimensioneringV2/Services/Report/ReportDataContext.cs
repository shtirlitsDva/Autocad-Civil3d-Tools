using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models;
using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.DataModels;

using NorsynHydraulicCalc;

using QuikGraph;

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

    public NetworkScope? Scope { get; set; }
    public required List<UndirectedGraph<NodeJunction, EdgePipeSegment>> OrderedGraphs { get; init; }
    public required ScopedReportData TotalData { get; init; }
    public required List<ScopedReportData> PerNetworkData { get; init; }

    public SystemSummary Summary { get; set; } = null!;
    public List<ComplianceRow> ComplianceChecks { get; set; } = null!;
    public List<SupplyPointRow> SupplyPoints { get; set; } = null!;

    /// <summary>
    /// Current top-level section number, set by the orchestrator before each module.
    /// Modules use this for dynamic section headings (e.g. "{CurrentSection}.1 Sub-title").
    /// </summary>
    public int CurrentSection { get; set; }

    public int SubSectionCounter { get; set; }
}
