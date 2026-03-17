using Autodesk.AutoCAD.ApplicationServices;

using DimensioneringV2.Models.Report;

using System;
using System.Linq;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Singleton service for managing report profiles.
/// Follows NyttetimerService pattern: loads/saves via SettingsSerializer to FlexDataStore.
/// </summary>
internal class ReportProfileService
{
    private static readonly Lazy<ReportProfileService> _instance = new(() => new ReportProfileService());
    public static ReportProfileService Instance => _instance.Value;

    public ReportProfileStore Store { get; private set; } = new();

    /// <summary>
    /// Gets the currently selected profile, or creates a default one if none exist.
    /// </summary>
    public ReportProfile CurrentProfile
    {
        get
        {
            var profile = Store.Profiles
                .FirstOrDefault(p => p.Name == Store.SelectedProfileName);

            if (profile == null)
            {
                profile = ReportProfile.CreateDefault();
                Store.Profiles.Add(profile);
                Store.SelectedProfileName = profile.Name;
            }

            return profile;
        }
    }

    private ReportProfileService() { }

    /// <summary>
    /// Loads report profiles from the active document's FlexDataStore.
    /// </summary>
    public void LoadFromActiveDocument()
    {
        var doc = AcAp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        Store = SettingsSerializer<ReportProfileStore>.Load(doc);

        // Ensure at least the default profile exists
        if (Store.Profiles.Count == 0)
        {
            Store.Profiles.Add(ReportProfile.CreateDefault());
            Store.SelectedProfileName = ReportProfile.DefaultProfileName;
        }
    }

    /// <summary>
    /// Saves report profiles to the active document's FlexDataStore.
    /// </summary>
    public void SaveToActiveDocument()
    {
        var doc = AcAp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        SettingsSerializer<ReportProfileStore>.Save(doc, Store);
    }

    public ReportProfile CreateProfile(string baseName)
    {
        var name = Store.GetUniqueName(baseName);
        var profile = ReportProfile.CreateDefault();
        profile.Name = name;
        Store.Profiles.Add(profile);
        return profile;
    }

    public ReportProfile DuplicateProfile(ReportProfile source)
    {
        var name = Store.GetUniqueName(source.Name + " kopi");
        var copy = source.Duplicate(name);
        Store.Profiles.Add(copy);
        return copy;
    }

    public void DeleteProfile(ReportProfile profile)
    {
        Store.Profiles.Remove(profile);
        if (Store.SelectedProfileName == profile.Name && Store.Profiles.Count > 0)
            Store.SelectedProfileName = Store.Profiles[0].Name;
    }

    public static void Reset()
    {
        if (_instance.IsValueCreated)
        {
            _instance.Value.Store = new ReportProfileStore();
        }
    }
}
