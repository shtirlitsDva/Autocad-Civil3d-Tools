using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Models.Report;

/// <summary>
/// A named report profile containing module toggles/ordering and general settings.
/// Persisted to FlexDataStore via ReportProfileStore.
/// </summary>
internal class ReportProfile
{
    public const string DefaultProfileName = "Standard";

    public string Name { get; set; } = DefaultProfileName;

    public List<ReportModuleEntry> Modules { get; set; } = new();

    /// <summary>
    /// Whether to show the full nyttetimer table or only codes present in the data.
    /// </summary>
    public bool ShowAllNyttetimerCodes { get; set; } = false;

    /// <summary>
    /// Norm- og regelgrundlag text (§4.1).
    /// </summary>
    public string? NormText { get; set; }

    /// <summary>
    /// Afvigelser / supplerende krav text (§4.2).
    /// </summary>
    public string? DeviationsText { get; set; }

    public ReportProfile() { }

    /// <summary>
    /// Creates a default profile with all modules in standard order.
    /// </summary>
    public static ReportProfile CreateDefault()
    {
        var profile = new ReportProfile { Name = DefaultProfileName };
        int order = 0;
        profile.Modules = new List<ReportModuleEntry>
        {
            new(ReportModuleId.CoverPage, true, order++),
            new(ReportModuleId.Summary, true, order++),
            new(ReportModuleId.ProjectBasis, true, order++),
            new(ReportModuleId.CalcPrerequisites, true, order++),
            new(ReportModuleId.SupplyPoints, true, order++),
            new(ReportModuleId.SystemResults, true, order++),
            new(ReportModuleId.Sensitivity, false, order++),
            new(ReportModuleId.SegmentResults, true, order++),
            new(ReportModuleId.NodeResults, true, order++),
            new(ReportModuleId.ConsumerOverview, true, order++),
            new(ReportModuleId.OverviewMap, false, order++),
        };
        return profile;
    }

    /// <summary>
    /// Creates a deep copy of this profile with a new name.
    /// </summary>
    public ReportProfile Duplicate(string newName)
    {
        return new ReportProfile
        {
            Name = newName,
            ShowAllNyttetimerCodes = ShowAllNyttetimerCodes,
            NormText = NormText,
            DeviationsText = DeviationsText,
            Modules = Modules.Select(m =>
                new ReportModuleEntry(m.ModuleId, m.IsEnabled, m.SortOrder)).ToList()
        };
    }
}
