using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanWidthCalculator
{
    public static bool TryResolveDrawingWidth(string layerName, out double widthMetres, out string error)
    {
        widthMetres = 0.0;
        error = string.Empty;

        if (NSPaletteAdapter.TryGetCurrentSeries(out PipeSeriesEnum series) == PipePaletteSeriesStatus.NotLoaded)
        {
            error = "NSPalette is not loaded. Open the palette and select a series first.";
            return false;
        }

        double kOdMillimeters = PipeScheduleV2.PipeScheduleV2.GetPipeKOd(layerName, series);
        if (kOdMillimeters <= 0.0)
        {
            error = $"No casing OD found for '{layerName}' at series {series}. " +
                    "Check the active layer and the palette's current series.";
            return false;
        }

        widthMetres = kOdMillimeters / 1000.0;
        return true;
    }
}
