using System.Reflection;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal enum PipePaletteSeriesStatus
{
    NotLoaded,
    Available,
}

internal static class NSPaletteAdapter
{
    private const string AssemblyName = "NSPalette";
    private const string PaletteUtilsTypeName = "NSPaletteSet.PaletteUtils";
    private const string CurrentSeriesProperty = "CurrentSeries";

    public static bool IsLoaded => FindAssembly() is not null;

    public static PipePaletteSeriesStatus TryGetCurrentSeries(out PipeSeriesEnum series)
    {
        series = PipeSeriesEnum.Undefined;
        Assembly? asm = FindAssembly();
        if (asm is null) return PipePaletteSeriesStatus.NotLoaded;

        Type? type = asm.GetType(PaletteUtilsTypeName);
        PropertyInfo? prop = type?.GetProperty(
            CurrentSeriesProperty,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop?.GetValue(null) is PipeSeriesEnum value)
        {
            series = value;
        }

        return PipePaletteSeriesStatus.Available;
    }

    private static Assembly? FindAssembly()
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(asm.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return asm;
            }
        }

        return null;
    }
}
