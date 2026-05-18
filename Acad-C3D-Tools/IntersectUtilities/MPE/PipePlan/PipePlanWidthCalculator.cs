using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanWidthCalculator
{
    public static double ResolveDrawingWidth(PipeSystemEnum system, PipeTypeEnum type, int dn, double fallback = 0.0)
    {
        PipeSeriesEnum series = NSPaletteAdapter.TryGetCurrentSeries(out PipeSeriesEnum s)
            ? s
            : PipeSeriesEnum.S3;

        try
        {
            double kOdMillimeters = PipeScheduleV2.PipeScheduleV2.GetPipeKOd(system, dn, type, series);
            if (kOdMillimeters > 0.0)
            {
                return kOdMillimeters / 1000.0;
            }
        }
        catch
        {
            // fall through to fallback
        }

        return fallback;
    }
}
