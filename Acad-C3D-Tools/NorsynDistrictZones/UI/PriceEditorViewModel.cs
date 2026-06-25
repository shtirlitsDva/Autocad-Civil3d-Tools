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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCatalogIsReadOnly))]
    [NotifyPropertyChangedFor(nameof(SelectedCatalogIsEditable))]
    [NotifyCanExecuteChangedFor(nameof(RenameCatalogCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCatalogCommand))]
    private PipePriceCatalog? selectedCatalog;

    [ObservableProperty] private string activeName = string.Empty;

    /// <summary>True for the built-in "Default" catalog — its prices, name and existence are locked.</summary>
    public bool SelectedCatalogIsReadOnly => SelectedCatalog?.IsReadOnly ?? false;
    public bool SelectedCatalogIsEditable => SelectedCatalog is { IsReadOnly: false };
    public bool CanDeleteCatalog => SelectedCatalogIsEditable && Catalogs.Count > 1;

    /// <summary>The View supplies the rename dialog: current name in, chosen name out (null ⇒ cancelled).</summary>
    public Func<string, string?>? RenameRequested;

    /// <summary>One collapsible table per pipe type, built dynamically from the catalog.</summary>
    public ObservableCollection<PipeTypePriceGroup> Groups { get; } = new();
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
        Groups.Clear();
        if (value is null) return;
        // Generic: one group per pipe type actually present in the catalog (no hardcoded list).
        // The entries are the SAME references, so in-grid edits mutate the catalog directly.
        foreach (var g in value.Entries.GroupBy(e => e.PipeType).OrderBy(g => g.Key))
            Groups.Add(new PipeTypePriceGroup(g.Key.ToString(), g.OrderBy(e => e.Dn)));
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

    [RelayCommand(CanExecute = nameof(SelectedCatalogIsEditable))]
    private void RenameCatalog()
    {
        if (SelectedCatalog is null || RenameRequested is null) return;
        string? proposed = RenameRequested(SelectedCatalog.Name);
        if (string.IsNullOrWhiteSpace(proposed)) return;

        string oldName = SelectedCatalog.Name;
        string newName = UniqueName(proposed.Trim(), SelectedCatalog);
        if (string.Equals(newName, oldName, StringComparison.Ordinal)) return;

        SelectedCatalog.Name = newName;
        if (string.Equals(ActiveName, oldName, StringComparison.OrdinalIgnoreCase)) ActiveName = newName;

        // PipePriceCatalog.Name is a plain model property — refresh the ComboBox's view so it redraws.
        System.Windows.Data.CollectionViewSource.GetDefaultView(Catalogs).Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteCatalog))]
    private void DeleteCatalog()
    {
        if (SelectedCatalog is null || SelectedCatalog.IsReadOnly || Catalogs.Count <= 1) return;
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

    private string UniqueName(string baseName, PipePriceCatalog? excluding = null)
    {
        string n = baseName;
        int i = 2;
        while (Catalogs.Any(c => !ReferenceEquals(c, excluding) && string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase)))
            n = $"{baseName} {i++}";
        return n;
    }
}

/// <summary>A pipe type's price rows, shown as one collapsible table in the editor.</summary>
public sealed class PipeTypePriceGroup
{
    public string Name { get; }
    public ObservableCollection<PipePriceEntry> Entries { get; }
    public string Header => $"{Name}   ({Entries.Count} dimensioner)";

    public PipeTypePriceGroup(string name, IEnumerable<PipePriceEntry> entries)
    {
        Name = name;
        Entries = new ObservableCollection<PipePriceEntry>(entries);
    }
}
