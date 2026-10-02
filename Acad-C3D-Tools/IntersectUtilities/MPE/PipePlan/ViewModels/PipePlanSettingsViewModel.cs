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
    private string _centreToCentreHeader = "c-c";

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

        PipeSeriesEnum series = NSPaletteAdapter.TryGetCurrentSeries(out PipeSeriesEnum current) && current != PipeSeriesEnum.Undefined
            ? current
            : PipeSeriesEnum.S2;
        CentreToCentreHeader = $"c-c {series}";

        foreach (PipePlanRadiusEntry entry in PipePlanRadiusStore.EnumerateAll(db))
        {
            // Bonded steel rows also carry the pair's min x; c-c follows for the series.
            Option<PipePlanPairRowData> pair = PipePlanPairSpacingResolver.IsBondedPair(entry.System, entry.Type)
                ? Option<PipePlanPairRowData>.Of(new PipePlanPairRowData(
                    PipePlanPairGapStore.Get(db, entry.System, entry.Dn),
                    PipeScheduleV2.PipeScheduleV2.GetPipeKOd(entry.System, entry.Dn, entry.Type, series)))
                : Option<PipePlanPairRowData>.Nothing;
            Entries.Add(new PipePlanRadiusEntryVm(entry, pair));
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
                if (row.IsMinXDirty)
                {
                    if (double.TryParse(row.MinXText, NumberStyles.Float, CultureInfo.InvariantCulture, out double minX) && minX > 0.0)
                    {
                        PipePlanPairGapStore.Set(doc.Database, row.System, row.Dn, minX);
                        updated++;
                    }
                    else
                    {
                        failed++;
                    }
                }

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
            if (row.HasMinX)
            {
                PipePlanPairGapStore.ResetToDefault(doc.Database, row.System, row.Dn);
            }
        }

        Reload();
        SetStatus($"Nulstillet {row.System} {row.Type} DN{row.Dn}.", PipePlanStatusKind.Info);
    }

    private static Database? GetActiveDatabase()
    {
        return Application.DocumentManager.MdiActiveDocument?.Database;
    }
}

/// <summary>A bonded row's spacing inputs: its min x and the jacket OD of the shown series.</summary>
internal sealed record PipePlanPairRowData(PipePlanMinX MinX, double JacketOdMm);

internal sealed partial class PipePlanRadiusEntryVm : ObservableObject
{
    private readonly double _originalRadius;
    private readonly Option<PipePlanPairRowData> _pair;

    public PipePlanRadiusEntryVm(PipePlanRadiusEntry entry, Option<PipePlanPairRowData> pair)
    {
        System = entry.System;
        Type = entry.Type;
        Dn = entry.Dn;
        _originalRadius = entry.Radius;
        _radiusText = entry.Radius > 0.0
            ? entry.Radius.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Empty;
        Source = entry.Source;
        _pair = pair;
        _minXText = pair.Match(p => p.MinX.Millimetres.ToString("0.###", CultureInfo.InvariantCulture), () => string.Empty);
    }

    public PipeSystemEnum System { get; }

    public PipeTypeEnum Type { get; }

    public int Dn { get; }

    public PipePlanRadiusSource Source { get; }

    /// <summary>Bonded steel rows: the radius is the inner pipe's and min x is editable.</summary>
    public bool HasMinX => _pair.Match(_ => true, () => false);

    // Hidden (not collapsed) so the columns stay aligned across rows.
    public global::System.Windows.Visibility MinXVisibility =>
        HasMinX ? global::System.Windows.Visibility.Visible : global::System.Windows.Visibility.Hidden;

    public string Label => $"{System} {Type} DN{Dn}";

    // Pair rows show radius/min x sources compactly, e.g. "api/ndh" or "ovr/ovr".
    public string SourceLabel => _pair.Match(
        p => $"{(Source == PipePlanRadiusSource.Override ? "ovr" : RadiusSourceLabel)}/" +
             (p.MinX.Source == PipePlanRadiusSource.Override ? "ovr" : "ndh"),
        () => RadiusSourceLabel);

    public string SourceColor => Source == PipePlanRadiusSource.Override ||
                                 _pair.Match(p => p.MinX.Source == PipePlanRadiusSource.Override, () => false)
        ? "#63B3ED"
        : Source == PipePlanRadiusSource.Missing ? "#ED8936" : "#A0AEC0";

    private string RadiusSourceLabel => Source switch
    {
        PipePlanRadiusSource.Override => "override",
        PipePlanRadiusSource.Default => "api",
        PipePlanRadiusSource.Missing => "missing",
    };

    [ObservableProperty]
    private string _radiusText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CentreToCentreText))]
    private string _minXText;

    /// <summary>kOd + min x of the series NSPalette shows (read-only).</summary>
    public string CentreToCentreText => _pair.Match(
        p => p.JacketOdMm > 0.0 &&
             double.TryParse(MinXText, NumberStyles.Float, CultureInfo.InvariantCulture, out double minX) && minX > 0.0
            ? (p.JacketOdMm + minX).ToString("0", CultureInfo.InvariantCulture)
            : "-",
        () => string.Empty);

    public bool IsMinXDirty => _pair.Match(
        p => double.TryParse(MinXText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? Math.Abs(value - p.MinX.Millimetres) > 1e-6
            : MinXText.Trim().Length > 0,
        () => false);

    public bool IsDirty
    {
        get
        {
            if (!double.TryParse(RadiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) return false;
            return Math.Abs(value - _originalRadius) > 1e-6 || Source == PipePlanRadiusSource.Missing;
        }
    }
}
