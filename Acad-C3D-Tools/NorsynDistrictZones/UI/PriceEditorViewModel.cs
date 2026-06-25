using System.Collections.ObjectModel;
using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Win32;

using NorsynDistrictZones.Pricing;

namespace NorsynDistrictZones.UI;

/// <summary>
/// MVVM for the standalone price editor: manage named catalogs (new / copy / delete /
/// rename / set-active / import / export) and edit per-(type,DN) prices. Edits stay in
/// memory until OK; the command then persists to the drawing and recomputes zones.
/// </summary>
public partial class PriceEditorViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<PipePriceCatalog> catalogs = new();
    [ObservableProperty] private PipePriceCatalog? selectedCatalog;
    [ObservableProperty] private string activeName = string.Empty;

    public ObservableCollection<PipePriceEntry> Entries { get; } = new();
    public bool Saved { get; private set; }
    public event Action? RequestClose;

    public PriceEditorViewModel(IEnumerable<PipePriceCatalog> cats, string active)
    {
        foreach (PipePriceCatalog c in cats) Catalogs.Add(c);
        ActiveName = active;
        SelectedCatalog = Catalogs.FirstOrDefault(c => c.Name == active) ?? Catalogs.FirstOrDefault();
    }

    partial void OnSelectedCatalogChanged(PipePriceCatalog? value)
    {
        Entries.Clear();
        if (value is null) return;
        foreach (PipePriceEntry e in value.Entries.OrderBy(x => x.PipeType).ThenBy(x => x.Dn))
            Entries.Add(e);
    }

    [RelayCommand]
    private void NewCatalog()
    {
        var c = PipePriceCatalog.SeedFromDefaults(UniqueName("New catalog"));
        Catalogs.Add(c);
        SelectedCatalog = c;
    }

    [RelayCommand]
    private void CopyCatalog()
    {
        if (SelectedCatalog is null) return;
        var c = SelectedCatalog.Copy(UniqueName(SelectedCatalog.Name + " copy"));
        Catalogs.Add(c);
        SelectedCatalog = c;
    }

    [RelayCommand]
    private void DeleteCatalog()
    {
        if (SelectedCatalog is null || Catalogs.Count <= 1) return;
        int i = Catalogs.IndexOf(SelectedCatalog);
        Catalogs.Remove(SelectedCatalog);
        SelectedCatalog = Catalogs[Math.Max(0, i - 1)];
    }

    [RelayCommand]
    private void MakeActive()
    {
        if (SelectedCatalog is not null) ActiveName = SelectedCatalog.Name;
    }

    [RelayCommand]
    private void Export()
    {
        if (SelectedCatalog is null) return;
        var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = SelectedCatalog.Name + ".json" };
        if (dlg.ShowDialog() == true)
            System.IO.File.WriteAllText(dlg.FileName,
                JsonSerializer.Serialize(SelectedCatalog, new JsonSerializerOptions { WriteIndented = true }));
    }

    [RelayCommand]
    private void Import()
    {
        var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var c = JsonSerializer.Deserialize<PipePriceCatalog>(System.IO.File.ReadAllText(dlg.FileName));
            if (c is null) return;
            c.Name = UniqueName(c.Name);
            Catalogs.Add(c);
            SelectedCatalog = c;
        }
        catch { /* malformed file — ignore */ }
    }

    [RelayCommand]
    private void Ok() { Saved = true; RequestClose?.Invoke(); }

    [RelayCommand]
    private void Cancel() { Saved = false; RequestClose?.Invoke(); }

    private string UniqueName(string baseName)
    {
        string n = baseName;
        int i = 2;
        while (Catalogs.Any(c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase)))
            n = $"{baseName} {i++}";
        return n;
    }
}
