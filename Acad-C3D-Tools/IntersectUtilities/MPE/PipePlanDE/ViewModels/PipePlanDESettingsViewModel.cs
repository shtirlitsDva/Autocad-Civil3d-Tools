using System.Collections.ObjectModel;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.MPE.PipePlan;

namespace IntersectUtilities.MPE.PipePlanDE.ViewModels;

/// <summary>
/// Drives the PDSETTINGS palette: the editable Regel-Grabenprofil parameter table
/// alongside the reference diagram. Pure per-drawing configuration — it neither knows
/// nor sets the active drawing DN (that lives in the PDDRAW size palette /
/// <see cref="PipePlanDEState"/>). Edits are persisted to the active drawing via
/// <see cref="PipePlanDEParameterStore"/>.
/// </summary>
internal sealed partial class PipePlanDESettingsViewModel : ObservableObject
{
    /// <summary>One editable row per parameter table band (all DNs, all columns).</summary>
    public ObservableCollection<PipePlanDEParamRowVm> Rows { get; } = new();

    public IReadOnlyList<string> ColumnLabels { get; } = PipePlanDEParameters.DisplayLabels;

    [ObservableProperty]
    private string _status = "Rediger parametre og gem.";

    [ObservableProperty]
    private string _statusColor = "#A0AEC0";

    public void Rebind() => LoadRows();

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

    [RelayCommand]
    public void Reload() => LoadRows();

    [RelayCommand]
    public void Save()
    {
        // Capture the document ONCE, then lock it and write to its database. Resolving
        // MdiActiveDocument a second time (or pairing a separately-fetched Database with
        // a freshly-resolved lock) could, in a modeless palette, write one drawing while
        // locking another. db comes from the locked document, so the two always agree.
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            SetStatus("Ingen aktiv tegning.", PipePlanStatusKind.Warning);
            return;
        }

        Database db = doc.Database;
        int saved = 0;
        int failed = 0;
        string? firstError = null;
        using (doc.LockDocument())
        {
            foreach (PipePlanDEParamRowVm row in Rows)
            {
                if (!row.HasEdits)
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

        // Only refresh from the store when everything saved cleanly. On any failure we
        // keep the grid as-is so the invalid input stays on screen for the user to
        // correct, instead of being silently overwritten by a reload.
        if (failed == 0)
        {
            LoadRows();
            SetStatus($"Gemt {saved} række(r).", PipePlanStatusKind.Ok);
        }
        else
        {
            SetStatus($"Gemt {saved}. {firstError} ({failed} afvist — ret og gem igen)", PipePlanStatusKind.Warning);
        }
    }

    [RelayCommand]
    public void ResetAll()
    {
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            SetStatus("Ingen aktiv tegning.", PipePlanStatusKind.Warning);
            return;
        }

        Database db = doc.Database;
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

internal sealed class PipePlanDEParamRowVm
{
    // The four editable inputs whose values feed the computed b / b1 columns.
    private readonly PipePlanDEParamCellVm _z13;
    private readonly PipePlanDEParamCellVm _d;
    private readonly PipePlanDEParamCellVm _x;
    private readonly PipePlanDEParamCellVm _z24;
    private readonly PipePlanDEParamCellVm _bComputed;
    private readonly PipePlanDEParamCellVm _b1Computed;
    private readonly PipePlanDEParamCellVm _bigB;
    private readonly PipePlanDEParamCellVm _bigB1;

    public PipePlanDEParamRowVm(PipePlanDEParameterEntry entry)
    {
        Dn = entry.Dn;
        Label = entry.Label;
        IsOverride = entry.IsOverride;

        PipePlanDEParameters p = entry.Parameters;
        // Display order: z1/z3, d, x, z2/z4, b (computed), b1 (computed), B, B1.
        _z13 = Input("z1/z3", p.Z1Z3);
        _d = Input("d", p.D);
        _x = Input("x", p.X);
        _z24 = Input("z2/z4", p.Z2Z4);
        _bComputed = Computed("b");
        _b1Computed = Computed("b1");
        _bigB = Input("B", p.B);
        _bigB1 = Input("B1", p.B1);

        Cells.Add(_z13);
        Cells.Add(_d);
        Cells.Add(_x);
        Cells.Add(_z24);
        Cells.Add(_bComputed);
        Cells.Add(_b1Computed);
        Cells.Add(_bigB);
        Cells.Add(_bigB1);

        // b and b1 are pure sums of the spacing inputs — recompute live as they change.
        foreach (PipePlanDEParamCellVm cell in new[] { _z13, _d, _x, _z24 })
        {
            cell.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PipePlanDEParamCellVm.ValueText))
                {
                    Recompute();
                }
            };
        }

        Recompute();
    }

    public int Dn { get; }

    public string Label { get; }

    public bool IsOverride { get; }

    public string SourceColor => IsOverride ? "#63B3ED" : "#A0AEC0";

    public ObservableCollection<PipePlanDEParamCellVm> Cells { get; } = new();

    /// <summary>The user edited at least one cell in this row (valid or not).</summary>
    public bool HasEdits => Cells.Any(c => c.IsChanged);

    public bool TryBuildParameters(out PipePlanDEParameters? parameters)
    {
        parameters = null;
        // Storage order: z1/z3, d, x, z2/z4, B, B1 (b/b1 are not stored).
        if (!TryParse(_z13, out double z13) ||
            !TryParse(_d, out double d) ||
            !TryParse(_x, out double x) ||
            !TryParse(_z24, out double z24) ||
            !TryParse(_bigB, out double bigB) ||
            !TryParse(_bigB1, out double bigB1))
        {
            return false;
        }

        parameters = new PipePlanDEParameters([z13, d, x, z24, bigB, bigB1]);
        return true;
    }

    private void Recompute()
    {
        if (TryParse(_z13, out double z13) && TryParse(_d, out double d)
            && TryParse(_x, out double x) && TryParse(_z24, out double z24))
        {
            double b = z13 + (2.0 * d) + x + z24;
            _bComputed.SetComputed(b);
            _b1Computed.SetComputed(b);
        }
        else
        {
            _bComputed.SetComputed(double.NaN);
            _b1Computed.SetComputed(double.NaN);
        }
    }

    private static PipePlanDEParamCellVm Input(string name, double value)
        => new(name, value, isComputed: false);

    private static PipePlanDEParamCellVm Computed(string name)
        => new(name, 0.0, isComputed: true);

    private static bool TryParse(PipePlanDEParamCellVm cell, out double value)
        => double.TryParse(cell.ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

internal sealed partial class PipePlanDEParamCellVm : ObservableObject
{
    private readonly double _original;
    private readonly string _originalText;

    public PipePlanDEParamCellVm(string name, double value, bool isComputed)
    {
        Name = name;
        IsComputed = isComputed;
        _original = value;
        _originalText = value.ToString("0.###", CultureInfo.InvariantCulture);
        _valueText = isComputed ? Format(value) : _originalText;
    }

    public string Name { get; }

    /// <summary>Read-only derived column (b, b1): not editable, never counts as changed.</summary>
    public bool IsComputed { get; }

    [ObservableProperty]
    private string _valueText;

    /// <summary>
    /// Edit-state: the user changed the text, whether or not it parses. Deliberately
    /// separate from numeric validity so an invalid entry is still recognised as a
    /// pending edit (and surfaced by Save) rather than silently dropped and overwritten.
    /// </summary>
    public bool IsChanged
    {
        get
        {
            if (IsComputed)
            {
                return false;
            }

            return double.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? Math.Abs(value - _original) > 1e-9
                : !string.Equals(ValueText, _originalText, StringComparison.Ordinal);
        }
    }

    /// <summary>Pushes a freshly recomputed value into a locked cell.</summary>
    public void SetComputed(double value) => ValueText = Format(value);

    private static string Format(double value)
        => double.IsNaN(value) ? "—" : value.ToString("0.###", CultureInfo.InvariantCulture);
}
