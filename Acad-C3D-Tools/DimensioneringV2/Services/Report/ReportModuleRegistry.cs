using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.Modules;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Static registry of all available report modules.
/// Modules are instantiated once and reused across report generations.
/// </summary>
internal static class ReportModuleRegistry
{
    private static readonly List<IReportModule> _modules = new()
    {
        new CoverPageModule(),
        new SummaryModule(),
        // Phase 4-5 modules will be added here as implemented
    };

    public static IReadOnlyList<IReportModule> AllModules => _modules;

    public static IReportModule? Get(ReportModuleId id) =>
        _modules.FirstOrDefault(m => m.Id == id);
}
