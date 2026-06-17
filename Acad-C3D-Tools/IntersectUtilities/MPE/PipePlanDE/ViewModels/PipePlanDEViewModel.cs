using System.Collections.ObjectModel;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.MPE.PipePlan;

namespace IntersectUtilities.MPE.PipePlanDE.ViewModels;

internal sealed partial class PipePlanDEViewModel : ObservableObject
{
    // Nullable because the palette is constructed up front (process-wide) and may
    // briefly exist before the first DocumentActivated rebind.
    private PipePlanDEState? _state;
    private bool _suppressSelectionSideEffects;

    public PipePlanDEViewModel()
    {
        foreach (int dn in PipePlanDEStandardTable.SelectableDns)
        {
            Dns.Add(new PipePlanDEDnVm(dn, $"DN {dn}"));
        }
    }

    public ObservableCollection<PipePlanDEDnVm> Dns { get; } = new();

    /// <summary>One editable row per parameter table band (all DNs, all columns).</summary>
    public ObservableCollection<PipePlanDEParamRowVm> Rows { get; } = new();

    public IReadOnlyList<string> ColumnLabels { get; } = PipePlanDEParameters.ColumnLabels;

    [ObservableProperty]
    private PipePlanDEDnVm? _selectedDn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AdvancedVisibility))]
    [NotifyPropertyChangedFor(nameof(AdvancedButtonText))]
    private bool _isAdvancedVisible;

    [ObservableProperty]
    private string _status = "Vælg en dimension.";

    [ObservableProperty]
    private string _statusColor = "#A0AEC0";

    public string AdvancedVisibility => IsAdvancedVisible ? "Visible" : "Collapsed";

    public string AdvancedButtonText => IsAdvancedVisible ? "Skjul tabel" : "Rediger tabel…";

    public void Rebind(PipePlanDEState state)
    {
        _state = state;
        RestoreSelectionFromState();
        LoadRows();
    }

    public void SetStatus(string message, PipePlanStatusKind kind)
    {
        Status = message;
        StatusColor = kind switch
        {
            PipePlanStatusKind.Ok => "#48BB78",
            PipePlanStatusKind.Snap => "#63B3ED",
            PipePlanStatusKind.Warning => "#ED8936",
            PipePlanStatusKind.Error => "#E53E3E",
            _ => "#A0AEC0"
        };
    }

    partial void OnSelectedDnChanged(PipePlanDEDnVm? value)
    {
        if (_state is not null)
        {
            _state.ActiveDn = value?.Dn;
        }

        if (_suppressSelectionSideEffects)
        {
            return;
        }

        if (value is not null)
        {
            SetStatus($"Aktiv dimension: DN {value.Dn}. Kør PDDRAW.", PipePlanStatusKind.Ok);
        }
    }

    [RelayCommand]
    public void ToggleAdvanced() => IsAdvancedVisible = !IsAdvancedVisible;

    [RelayCommand]
    public void Reload()
    {
        RestoreSelectionFromState();
        LoadRows();
    }

    [RelayCommand]
    public void Save()
    {
        Database? db = GetActiveDatabase();
        if (db is null)
        {
            SetStatus("Ingen aktiv tegning.", PipePlanStatusKind.Warning);
            return;
        }

        int saved = 0;
        int failed = 0;
        string? firstError = null;
        Document doc = Application.DocumentManager.MdiActiveDocument;
        using (doc.LockDocument())
        {
            foreach (PipePlanDEParamRowVm row in Rows)
            {
                if (!row.IsDirty)
                {
                    continue;
                }

                if (!row.TryBuildParameters(out PipePlanDEParameters? parameters) || parameters is null)
                {
                    failed++;
                    firstError ??= $"DN {row.Dn}: ugyldigt tal.";
                    continue;
                }

                if (!parameters.TryValidate(out string validationError))
                {
                    failed++;
                    firstError ??= $"DN {row.Dn}: {validationError}";
                    continue;
                }

                PipePlanDEParameterStore.Set(db, row.Dn, parameters);
                saved++;
            }
        }

        LoadRows();
        SetStatus(
            failed == 0 ? $"Gemt {saved} række(r)." : $"Gemt {saved}. {firstError} ({failed} afvist)",
            failed == 0 ? PipePlanStatusKind.Ok : PipePlanStatusKind.Warning);
    }

    [RelayCommand]
    public void ResetAll()
    {
        Database? db = GetActiveDatabase();
        if (db is null)
        {
            return;
        }

        Document doc = Application.DocumentManager.MdiActiveDocument;
        using (doc.LockDocument())
        {
            foreach (PipePlanDEStandardRow row in PipePlanDEStandardTable.Rows)
            {
                PipePlanDEParameterStore.ResetToDefault(db, row.Dn);
            }
        }

        LoadRows();
        SetStatus("Alle rækker nulstillet til standard.", PipePlanStatusKind.Info);
    }

    private void RestoreSelectionFromState()
    {
        int? activeDn = _state?.ActiveDn;
        _suppressSelectionSideEffects = true;
        try
        {
            SelectedDn = activeDn is null
                ? null
                : Dns.FirstOrDefault(d => d.Dn == activeDn.Value);
        }
        finally
        {
            _suppressSelectionSideEffects = false;
        }
    }

    private void LoadRows()
    {
        Rows.Clear();
        Database? db = GetActiveDatabase();

        IReadOnlyList<PipePlanDEParameterEntry> entries = db is not null
            ? PipePlanDEParameterStore.EnumerateAll(db)
            : PipePlanDEStandardTable.Rows
                .Select(r => new PipePlanDEParameterEntry(r.Dn, r.Label, r.Parameters, IsOverride: false))
                .ToList();

        foreach (PipePlanDEParameterEntry entry in entries)
        {
            Rows.Add(new PipePlanDEParamRowVm(entry));
        }
    }

    private static Database? GetActiveDatabase()
        => Application.DocumentManager.MdiActiveDocument?.Database;
}

internal sealed class PipePlanDEDnVm
{
    public PipePlanDEDnVm(int dn, string label)
    {
        Dn = dn;
        Label = label;
    }

    public int Dn { get; }

    public string Label { get; }
}

internal sealed class PipePlanDEParamRowVm
{
    public PipePlanDEParamRowVm(PipePlanDEParameterEntry entry)
    {
        Dn = entry.Dn;
        Label = entry.Label;
        IsOverride = entry.IsOverride;
        for (int i = 0; i < PipePlanDEParameters.ColumnCount; i++)
        {
            Cells.Add(new PipePlanDEParamCellVm(PipePlanDEParameters.ColumnLabels[i], entry.Parameters[i]));
        }
    }

    public int Dn { get; }

    public string Label { get; }

    public bool IsOverride { get; }

    public string SourceColor => IsOverride ? "#63B3ED" : "#A0AEC0";

    public ObservableCollection<PipePlanDEParamCellVm> Cells { get; } = new();

    public bool IsDirty => Cells.Any(c => c.IsDirty);

    public bool TryBuildParameters(out PipePlanDEParameters? parameters)
    {
        parameters = null;
        double[] values = new double[PipePlanDEParameters.ColumnCount];
        for (int i = 0; i < Cells.Count; i++)
        {
            if (!double.TryParse(Cells[i].ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return false;
            }

            values[i] = value;
        }

        parameters = new PipePlanDEParameters(values);
        return true;
    }
}

internal sealed partial class PipePlanDEParamCellVm : ObservableObject
{
    private readonly double _original;

    public PipePlanDEParamCellVm(string name, double value)
    {
        Name = name;
        _original = value;
        _valueText = value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public string Name { get; }

    [ObservableProperty]
    private string _valueText;

    public bool IsDirty
        => double.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
           && Math.Abs(value - _original) > 1e-9;
}
