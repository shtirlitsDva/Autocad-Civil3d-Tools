using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanLayerResolver
{
    public static bool TryResolve(Database db, out PipePlanActiveContext? context, out string error)
    {
        context = null;
        error = string.Empty;

        if (!TryReadActiveLayerName(db, out string layerName))
        {
            error = "Could not read the active layer.";
            return false;
        }

        PipeTypeEnum type = PipeScheduleV2.PipeScheduleV2.GetPipeType(layerName);
        PipeSystemEnum system = PipeScheduleV2.PipeScheduleV2.GetPipeSystem(layerName);
        int dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(layerName);

        if (system == PipeSystemEnum.Ukendt || type == PipeTypeEnum.Ukendt || dn <= 0)
        {
            error = $"PipePlan needs an active FJV layer. Active layer: '{layerName}'. Pick a size in NSPalette first.";
            return false;
        }

        if (!PipePlanRadiusStore.IsAcceptedCombo(system, type))
        {
            error = $"PipePlan supports twin bonded-steel (FJV-TWIN-DN…) and ALUPEX layers only. Active layer: '{layerName}' ({system} {type}).";
            return false;
        }

        if (!PipePlanRadiusStore.TryGet(db, system, type, dn, out double radius))
        {
            error = $"No bending radius set for {system} {type} DN{dn}. Open PPSETTINGS and set it first.";
            return false;
        }

        double width = ResolveWidth(system, type, dn);
        context = new PipePlanActiveContext(system, type, dn, width, radius, layerName);
        return true;
    }

    private static bool TryReadActiveLayerName(Database db, out string layerName)
    {
        layerName = string.Empty;
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            LayerTableRecord layer = (LayerTableRecord)tx.GetObject(db.Clayer, OpenMode.ForRead);
            layerName = layer.Name;
            tx.Commit();
            return !string.IsNullOrWhiteSpace(layerName);
        }
        catch
        {
            tx.Abort();
            return false;
        }
    }

    private static double ResolveWidth(PipeSystemEnum system, PipeTypeEnum type, int dn)
    {
        PipeSeriesEnum series = NSPaletteAdapter.TryGetCurrentSeries(out PipeSeriesEnum s)
            ? s
            : PipeSeriesEnum.S3;

        try
        {
            double kOd = PipeScheduleV2.PipeScheduleV2.GetPipeKOd(system, dn, type, series);
            if (kOd > 0.0) return kOd;
        }
        catch
        {
            // fall through to default
        }

        return 0.0;
    }
}
