using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>Where a pair's spacing comes from: c-c = kOd(DN, series) + min x.</summary>
internal static class PipePlanPairSpacingResolver
{
    /// <summary>Only steel draws FREM/RETUR as a pair; AluPex Frem/Retur stay single.</summary>
    public static bool IsBondedPair(PipeSystemEnum system, PipeTypeEnum type) =>
        system == PipeSystemEnum.Stål &&
        (type == PipeTypeEnum.Frem || type == PipeTypeEnum.Retur || type == PipeTypeEnum.Enkelt);

    /// <summary>Draw time: NSPalette's current series, S2 when it cannot be read.</summary>
    public static Result<PipePlanPairSpacing> ForNewDraft(Database db, PipeSystemEnum system, int dn)
    {
        PipeSeriesEnum series = NSPaletteAdapter.TryGetCurrentSeries(out PipeSeriesEnum current) && current != PipeSeriesEnum.Undefined
            ? current
            : PipeSeriesEnum.S2;
        return ForSeries(db, system, dn, series);
    }

    public static Result<PipePlanPairSpacing> ForSeries(Database db, PipeSystemEnum system, int dn, PipeSeriesEnum series)
    {
        double jacketOdMm = PipeScheduleV2.PipeScheduleV2.GetPipeKOd(system, dn, PipeTypeEnum.Enkelt, series);
        if (jacketOdMm <= 0.0)
        {
            return Result<PipePlanPairSpacing>.Failure($"Ingen kappediameter for {system} Enkelt DN{dn} {series}.");
        }

        double minXMm = PipePlanPairGapStore.Get(db, system, dn).Millimetres;
        return Result<PipePlanPairSpacing>.Success(new PipePlanPairSpacing(series, jacketOdMm, minXMm));
    }

    /// <summary>
    /// Re-solve time: the series is read from the pipes' CURRENT jacket width (so a
    /// "Polylinjer bredde opdater" series change carries through), min x from the current
    /// PPSETTINGS value. Falls back to what the run was baked with.
    /// </summary>
    public static PipePlanPairSpacing ForExistingRun(Database db, PipePlanPairStoredData data, IEnumerable<Polyline> pipes)
    {
        PipeSeriesEnum series = pipes
            .Select(pipe => PipeScheduleV2.PipeScheduleV2.GetPipeSeriesV2(pipe))
            .FirstOrDefault(s => s != PipeSeriesEnum.Undefined);
        if (series == PipeSeriesEnum.Undefined)
        {
            series = data.Spacing.Series;
        }

        return ForSeries(db, data.System, data.Dn, series).Match(spacing => spacing, _ => data.Spacing);
    }
}
