using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>What a new PPDRAW draft draws: the active size and whether it is a pair.</summary>
internal sealed record PipePlanDraftSetup(PipePlanActiveContext Context, PipePlanShape Shape);

internal static class PipePlanLayerResolver
{
    /// <summary>
    /// Reads the active FJV layer set by NSPalette. A steel FREM or RETUR layer means a
    /// bonded pair (the drafter draws its centreline); everything PipePlan accepts besides
    /// draws one polyline.
    /// </summary>
    public static Result<PipePlanDraftSetup> Resolve(Database db)
    {
        if (!TryReadActiveLayerName(db, out string layerName))
        {
            return Result<PipePlanDraftSetup>.Failure("Kan ikke læse det aktive lag.");
        }

        PipeTypeEnum type = PipeScheduleV2.PipeScheduleV2.GetPipeType(layerName);
        PipeSystemEnum system = PipeScheduleV2.PipeScheduleV2.GetPipeSystem(layerName);
        int dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(layerName);

        if (system == PipeSystemEnum.Ukendt || type == PipeTypeEnum.Ukendt || dn <= 0)
        {
            return Result<PipePlanDraftSetup>.Failure($"Intet aktivt FJV-lag ('{layerName}'). Vælg en dimension i NSPalette.");
        }

        return PipePlanPairSpacingResolver.IsBondedPair(system, type)
            ? ResolvePair(db, system, dn)
            : ResolveSingle(db, system, type, dn, layerName);
    }

    private static Result<PipePlanDraftSetup> ResolveSingle(Database db, PipeSystemEnum system, PipeTypeEnum type, int dn, string layerName)
    {
        if (!PipePlanRadiusStore.IsSinglePipeCombo(system, type))
        {
            return Result<PipePlanDraftSetup>.Failure($"Laget '{layerName}' understøttes ikke. Skift til Stål Twin, Stål Frem/Retur eller ALUPEX i NSPalette.");
        }

        if (!PipePlanRadiusStore.TryGet(db, system, type, dn, out double radius))
        {
            return Result<PipePlanDraftSetup>.Failure($"Ingen bukkeradius for {system} {type} DN{dn}. Sæt den i PPSETTINGS.");
        }

        return Result<PipePlanDraftSetup>.Success(new PipePlanDraftSetup(
            new PipePlanActiveContext(system, type, dn, radius, layerName),
            PipePlanShape.Single));
    }

    private static Result<PipePlanDraftSetup> ResolvePair(Database db, PipeSystemEnum system, int dn)
    {
        if (!PipePlanRadiusStore.TryGet(db, system, PipeTypeEnum.Enkelt, dn, out double radius))
        {
            return Result<PipePlanDraftSetup>.Failure($"Ingen bukkeradius for {system} Enkelt DN{dn}. Sæt den i PPSETTINGS.");
        }

        return PipePlanPairSpacingResolver.ForNewDraft(db, system, dn).Map(spacing => new PipePlanDraftSetup(
            new PipePlanActiveContext(
                system,
                PipeTypeEnum.Enkelt,
                dn,
                radius,
                PipePlanPairWriter.LayerFor(PipePlanPairRole.Frem, system, dn)),
            new PipePlanShape.BondedPair(system, dn, spacing, Flip: false, FlipLocked: false)));
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
