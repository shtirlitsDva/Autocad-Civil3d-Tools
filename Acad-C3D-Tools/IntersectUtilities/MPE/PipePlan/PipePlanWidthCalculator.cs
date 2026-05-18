using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanWidthCalculator
{
    public static double ResolveDrawingWidth(string layerName)
    {
        NSPaletteAdapter.TryGetCurrentSeries(out PipeSeriesEnum series);
        double kOdMillimeters = PipeScheduleV2.PipeScheduleV2.GetPipeKOd(layerName, series);
        return kOdMillimeters > 0.0 ? kOdMillimeters / 1000.0 : 0.0;
    }
}
