using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report;

using System.Collections.ObjectModel;
using System.Linq;

namespace DimensioneringV2.UI.ReportSettings;

internal partial class ReportSettingsViewModel : ObservableObject
{
    private readonly ReportProfileService _service = ReportProfileService.Instance;

    public ObservableCollection<ReportProfile> Profiles { get; }

    [ObservableProperty]
    private ReportProfile? selectedProfile;

    [ObservableProperty]
    private ObservableCollection<ModuleToggleItem> moduleItems = new();

    [ObservableProperty]
    private string? normText;

    [ObservableProperty]
    private string? deviationsText;

    [ObservableProperty]
    private bool showAllNyttetimerCodes;

    public ReportSettingsViewModel()
    {
        Profiles = new ObservableCollection<ReportProfile>(_service.Store.Profiles);

        SelectedProfile = Profiles
            .FirstOrDefault(p => p.Name == _service.Store.SelectedProfileName)
            ?? Profiles.FirstOrDefault();
    }

    partial void OnSelectedProfileChanged(ReportProfile? value)
    {
        if (value == null) return;
        _service.Store.SelectedProfileName = value.Name;
        LoadProfileIntoUI(value);
    }

    private void LoadProfileIntoUI(ReportProfile profile)
    {
        NormText = profile.NormText;
        DeviationsText = profile.DeviationsText;
        ShowAllNyttetimerCodes = profile.ShowAllNyttetimerCodes;

        var items = profile.Modules
            .OrderBy(m => m.SortOrder)
            .Select(m => new ModuleToggleItem(m))
            .ToList();
        ModuleItems = new ObservableCollection<ModuleToggleItem>(items);
    }

    private void SaveUIIntoProfile()
    {
        if (SelectedProfile == null) return;
        SelectedProfile.NormText = NormText;
        SelectedProfile.DeviationsText = DeviationsText;
        SelectedProfile.ShowAllNyttetimerCodes = ShowAllNyttetimerCodes;

        // Module toggles are already bound to the underlying entries
    }

    [RelayCommand]
    private void NewProfile()
    {
        var profile = _service.CreateProfile("Ny profil");
        Profiles.Add(profile);
        SelectedProfile = profile;
    }

    [RelayCommand]
    private void DuplicateProfile()
    {
        if (SelectedProfile == null) return;
        SaveUIIntoProfile();
        var copy = _service.DuplicateProfile(SelectedProfile);
        Profiles.Add(copy);
        SelectedProfile = copy;
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile == null || Profiles.Count <= 1) return;
        var toDelete = SelectedProfile;
        _service.DeleteProfile(toDelete);
        Profiles.Remove(toDelete);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    [RelayCommand]
    private void MoveUp()
    {
        var selected = ModuleItems.FirstOrDefault(m => m.IsSelected);
        if (selected == null) return;
        int idx = ModuleItems.IndexOf(selected);
        if (idx <= 0) return;
        ModuleItems.Move(idx, idx - 1);
        UpdateSortOrders();
    }

    [RelayCommand]
    private void MoveDown()
    {
        var selected = ModuleItems.FirstOrDefault(m => m.IsSelected);
        if (selected == null) return;
        int idx = ModuleItems.IndexOf(selected);
        if (idx < 0 || idx >= ModuleItems.Count - 1) return;
        ModuleItems.Move(idx, idx + 1);
        UpdateSortOrders();
    }

    private void UpdateSortOrders()
    {
        for (int i = 0; i < ModuleItems.Count; i++)
            ModuleItems[i].Entry.SortOrder = i;
    }

    public void ApplyAndClose()
    {
        SaveUIIntoProfile();
    }
}

internal partial class ModuleToggleItem : ObservableObject
{
    internal ReportModuleEntry Entry { get; }

    public string DisplayName { get; }

    [ObservableProperty]
    private bool isEnabled;

    [ObservableProperty]
    private bool isSelected;

    public ModuleToggleItem(ReportModuleEntry entry)
    {
        Entry = entry;
        DisplayName = GetDisplayName(entry.ModuleId);
        IsEnabled = entry.IsEnabled;
    }

    partial void OnIsEnabledChanged(bool value) => Entry.IsEnabled = value;

    private static string GetDisplayName(ReportModuleId id) => id switch
    {
        ReportModuleId.CoverPage => "Forside",
        ReportModuleId.Summary => "Sammenfatning",
        ReportModuleId.ProjectBasis => "Projektgrundlag",
        ReportModuleId.CalcPrerequisites => "Beregningsforudsætninger",
        ReportModuleId.SupplyPoints => "Forsyningspunkter",
        ReportModuleId.SystemResults => "Systemresultater",
        ReportModuleId.Sensitivity => "Følsomhedsanalyse (placeholder)",
        ReportModuleId.SegmentResults => "Strækningsresultater",
        ReportModuleId.NodeResults => "Knudepunkter",
        ReportModuleId.ConsumerOverview => "Forbrugeroversigt",
        ReportModuleId.OverviewMap => "Oversigtskort (placeholder)",
        _ => id.ToString(),
    };
}
