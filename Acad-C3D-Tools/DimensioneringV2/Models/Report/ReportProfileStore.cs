using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Models.Report;

/// <summary>
/// Container for all report profiles in a document.
/// Persisted to FlexDataStore via SettingsSerializer.
/// </summary>
internal partial class ReportProfileStore : ObservableObject
{
    [ObservableProperty]
    private string selectedProfileName = ReportProfile.DefaultProfileName;

    public List<ReportProfile> Profiles { get; set; } = new();

    public ReportProfileStore() { }

    /// <summary>
    /// Gets a unique name for a new profile, adding numeric suffix if needed.
    /// </summary>
    public string GetUniqueName(string baseName)
    {
        var existingNames = Profiles
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        existingNames.Add(ReportProfile.DefaultProfileName);

        if (!existingNames.Contains(baseName))
            return baseName;

        int suffix = 1;
        string candidate;
        do
        {
            candidate = $"{baseName} ({suffix})";
            suffix++;
        } while (existingNames.Contains(candidate));

        return candidate;
    }
}
