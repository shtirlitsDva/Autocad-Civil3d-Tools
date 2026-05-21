using System.Collections.ObjectModel;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan.ViewModels;

internal sealed partial class PipePlanSettingsViewModel : ObservableObject
{
    // Nullable because the palette is constructed up front (process-wide) and may
    // briefly exist before the first DocumentActivated rebind. Operations that
    // need state (Save, Reload row sync) early-out gracefully when null.
    private PipePlanState? _state;

    public PipePlanSettingsViewModel()
    {
    }

    public void Rebind(PipePlanState state)
    {
        _state = state;
        StraightSnapTolerance = state.StraightSnapToleranceText;
        Reload();
    }

    public ObservableCollection<PipePlanRadiusEntryVm> Entries { get; } = new();

    [ObservableProperty]
    private string _straightSnapTolerance = "5";

    [ObservableProperty]
    private string _status = "Klar.";

    [ObservableProperty]
    private string _statusColor = "#A0AEC0";

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
    public void Reload()
    {
        Entries.Clear();
        Database? db = GetActiveDatabase();
        if (db is null)
        {
            SetStatus("Ingen aktiv tegning.", PipePlanStatusKind.Warning);
            return;
        }

        foreach (PipePlanRadiusEntry entry in PipePlanRadiusStore.EnumerateAll(db))
        {
            Entries.Add(new PipePlanRadiusEntryVm(entry));
        }

        if (_state is not null)
        {
            StraightSnapTolerance = _state.StraightSnapToleranceText;
        }
        SetStatus("Indlæst.", PipePlanStatusKind.Info);
    }

    [RelayCommand]
    public void Save()
    {
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            SetStatus("Ingen aktiv tegning.", PipePlanStatusKind.Warning);
            return;
        }

        if (!double.TryParse(StraightSnapTolerance, NumberStyles.Float, CultureInfo.InvariantCulture, out double tolerance) || tolerance <= 0.0)
        {
            SetStatus("Lige-snap-tolerance skal være positiv.", PipePlanStatusKind.Error);
            return;
        }

        if (_state is not null)
        {
            _state.StraightSnapToleranceText = StraightSnapTolerance;
        }

        int updated = 0;
        int failed = 0;
        using (doc.LockDocument())
        {
            foreach (PipePlanRadiusEntryVm row in Entries)
            {
                if (!row.IsDirty) continue;

                if (!double.TryParse(row.RadiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value <= 0.0)
                {
                    failed++;
                    continue;
                }

                PipePlanRadiusStore.Set(doc.Database, row.System, row.Type, row.Dn, value);
                updated++;
            }
        }

        Reload();
        SetStatus(
            failed == 0 ? $"Gemt {updated} override(s)." : $"Gemt {updated}, {failed} ugyldige.",
            failed == 0 ? PipePlanStatusKind.Ok : PipePlanStatusKind.Warning);
    }

    [RelayCommand]
    public void ResetRow(PipePlanRadiusEntryVm? row)
    {
        if (row is null) return;
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null) return;

        using (doc.LockDocument())
        {
            PipePlanRadiusStore.ResetToDefault(doc.Database, row.System, row.Type, row.Dn);
        }

        Reload();
        SetStatus($"Nulstillet {row.System} {row.Type} DN{row.Dn}.", PipePlanStatusKind.Info);
    }

    private static Database? GetActiveDatabase()
    {
        return Application.DocumentManager.MdiActiveDocument?.Database;
    }
}

internal sealed partial class PipePlanRadiusEntryVm : ObservableObject
{
    private readonly double _originalRadius;

    public PipePlanRadiusEntryVm(PipePlanRadiusEntry entry)
    {
        System = entry.System;
        Type = entry.Type;
        Dn = entry.Dn;
        _originalRadius = entry.Radius;
        _radiusText = entry.Radius > 0.0
            ? entry.Radius.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Empty;
        Source = entry.Source;
    }

    public PipeSystemEnum System { get; }

    public PipeTypeEnum Type { get; }

    public int Dn { get; }

    public PipePlanRadiusSource Source { get; }

    public string Label => $"{System} {Type} DN{Dn}";

    public string SourceLabel => Source switch
    {
        PipePlanRadiusSource.Override => "override",
        PipePlanRadiusSource.Default => "api",
        _ => "missing"
    };

    public string SourceColor => Source switch
    {
        PipePlanRadiusSource.Override => "#63B3ED",
        PipePlanRadiusSource.Default => "#A0AEC0",
        _ => "#ED8936"
    };

    [ObservableProperty]
    private string _radiusText;

    public bool IsDirty
    {
        get
        {
            if (!double.TryParse(RadiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) return false;
            return Math.Abs(value - _originalRadius) > 1e-6 || Source == PipePlanRadiusSource.Missing;
        }
    }
}
