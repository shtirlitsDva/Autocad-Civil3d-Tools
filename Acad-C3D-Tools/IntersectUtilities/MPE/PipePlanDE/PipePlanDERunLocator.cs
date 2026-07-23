using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Re-groups the three polylines of a PDDRAW run (centreline + frem + retur) from any one
/// of them, using the shared V2 run token. Needed by PDEDIT, which lets the user pick any
/// of the three but must find and re-bake all three together.
/// </summary>
internal static class PipePlanDERunLocator
{
    /// <summary>
    /// From any polyline of a run, resolves the three sibling polylines and the centreline's
    /// authoring data. Fails (with a Danish message) for V1/no-token runs — those predate the
    /// editable metadata and must be redrawn.
    /// </summary>
    public static bool TryFindRun(
        Database db,
        Transaction tx,
        ObjectId anyId,
        out ObjectId centreId,
        out ObjectId fremId,
        out ObjectId returId,
        out PipePlanDEStoredData? centreData,
        out string error)
    {
        centreId = ObjectId.Null;
        fremId = ObjectId.Null;
        returId = ObjectId.Null;
        centreData = null;
        error = string.Empty;

        if (tx.GetObject(anyId, OpenMode.ForRead) is not Polyline picked)
        {
            error = "Ikke en polylinje.";
            return false;
        }

        if (!PipePlanDEMetadata.TryRead(picked, tx, out PipePlanDEStoredData? pickedData) || pickedData is null)
        {
            error = "Ikke en PipePlanDE-polylinje (ingen metadata).";
            return false;
        }

        if (string.IsNullOrEmpty(pickedData.Token))
        {
            error = "Denne streg blev tegnet før redigering var muligt. Tegn den igen for at kunne redigere.";
            return false;
        }

        string token = pickedData.Token!;
        BlockTable blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in modelSpace)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Polyline pl)
            {
                continue;
            }

            if (!PipePlanDEMetadata.TryRead(pl, tx, out PipePlanDEStoredData? data) || data is null || data.Token != token)
            {
                continue;
            }

            switch (data.Role)
            {
                case PipePlanDERole.Centerline:
                    centreId = id;
                    centreData = data;
                    break;
                case PipePlanDERole.Supply:
                    fremId = id;
                    break;
                case PipePlanDERole.Return:
                    returId = id;
                    break;
            }
        }

        if (centreId.IsNull || fremId.IsNull || returId.IsNull)
        {
            error = "Kunne ikke finde alle tre rør i løbet. Tegn det igen.";
            return false;
        }

        if (centreData?.Authoring is null)
        {
            error = "Løbet mangler redigerbare data (kontrolpunkter). Tegn det igen.";
            return false;
        }

        if (centreData.Authoring.Straight)
        {
            error = "Løb tegnet uden buk (Straight) kan ikke redigeres. Tegn det igen med buk.";
            return false;
        }

        return true;
    }

    /// <summary>Erases the three polylines of a run (used by PDEDIT commit before re-baking).</summary>
    public static void EraseRun(Transaction tx, ObjectId centreId, ObjectId fremId, ObjectId returId)
    {
        foreach (ObjectId id in new[] { centreId, fremId, returId })
        {
            if (id.IsNull)
            {
                continue;
            }

            if (tx.GetObject(id, OpenMode.ForWrite) is Entity entity && !entity.IsErased)
            {
                entity.Erase();
            }
        }
    }
}
