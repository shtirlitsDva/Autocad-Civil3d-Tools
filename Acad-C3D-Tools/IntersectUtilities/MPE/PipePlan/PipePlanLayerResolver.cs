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
            error = "Kan ikke læse det aktive lag.";
            return false;
        }

        PipeTypeEnum type = PipeScheduleV2.PipeScheduleV2.GetPipeType(layerName);
        PipeSystemEnum system = PipeScheduleV2.PipeScheduleV2.GetPipeSystem(layerName);
        int dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(layerName);

        if (system == PipeSystemEnum.Ukendt || type == PipeTypeEnum.Ukendt || dn <= 0)
        {
            error = $"Intet aktivt FJV-lag ('{layerName}'). Vælg en dimension i NSPalette.";
            return false;
        }

        if (IsEnkeltPipe(system, type))
        {
            error = "Enkelt-rør understøttes ikke. Brug Stål Twin eller ALUPEX.";
            return false;
        }

        if (!PipePlanRadiusStore.IsAcceptedCombo(system, type))
        {
            error = $"Laget '{layerName}' understøttes ikke. Skift til Stål Twin eller ALUPEX i NSPalette.";
            return false;
        }

        if (!PipePlanRadiusStore.TryGet(db, system, type, dn, out double radius))
        {
            error = $"Ingen bukkeradius for {system} {type} DN{dn}. Sæt den i PPSETTINGS.";
            return false;
        }

        context = new PipePlanActiveContext(system, type, dn, radius, layerName);
        return true;
    }

    private static bool IsEnkeltPipe(PipeSystemEnum system, PipeTypeEnum type)
    {
        if (type == PipeTypeEnum.Enkelt) return true;
        if (system == PipeSystemEnum.Stål && (type == PipeTypeEnum.Frem || type == PipeTypeEnum.Retur)) return true;
        return false;
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

}
