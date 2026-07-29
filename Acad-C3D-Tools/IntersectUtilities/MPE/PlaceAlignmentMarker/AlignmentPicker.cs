using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.MPE.PlaceAlignmentMarker;

/// <summary>
/// One selectable alignment. <see cref="Database"/> is the database the alignment actually lives in
/// — the host drawing or an xref — and <see cref="TransformToHost"/> maps its geometry into host
/// WCS (identity for host alignments, the xref's <c>BlockTransform</c> otherwise).
/// </summary>
internal sealed record AlignmentSource(
    string DisplayName,
    Database Database,
    Oid AlignmentId,
    Matrix3d TransformToHost);

/// <summary>
/// Picks an alignment by name from a <see cref="StringGridForm"/> list — the same approach as
/// FINDALIGNMENT — instead of by clicking an entity. Selecting by name is what makes xreffed
/// alignments reachable: they are not selectable in the host drawing, but they are enumerable
/// through the xref's database.
/// </summary>
internal static class AlignmentPicker
{
    /// <summary>Guard against pathological xref nesting; AutoCAD forbids true cycles.</summary>
    private const int MaxXrefDepth = 8;

    /// <summary>
    /// Shows the name list and returns the chosen alignment, or null when nothing was found or the
    /// user cancelled. <paramref name="message"/> is reported when the result is null.
    /// </summary>
    internal static AlignmentSource? Pick(Database hostDb, out string message)
    {
        List<AlignmentSource> sources = Collect(hostDb);
        if (sources.Count == 0)
        {
            message = "Ingen alignments fundet — hverken i tegningen eller i dens xrefs.";
            return null;
        }

        MakeDisplayNamesUnique(sources);

        string? selected = StringGridFormCaller.Call(
            sources.Select(x => x.DisplayName).OrderBy(x => x),
            "SELECT ALIGNMENT:");

        if (selected is null)
        {
            message = "Annulleret.";
            return null;
        }

        AlignmentSource? source = sources.FirstOrDefault(x => x.DisplayName == selected);
        if (source is null)
        {
            message = $"Kunne ikke finde alignment '{selected}'.";
            return null;
        }

        message = string.Empty;
        return source;
    }

    private static List<AlignmentSource> Collect(Database hostDb)
    {
        List<AlignmentSource> sources = [];

        using Transaction tx = hostDb.TransactionManager.StartTransaction();
        CollectFrom(hostDb, tx, Matrix3d.Identity, 0, sources);
        tx.Commit();

        return sources;
    }

    private static void CollectFrom(
        Database db,
        Transaction tx,
        Matrix3d toHost,
        int depth,
        List<AlignmentSource> sources)
    {
        foreach (Alignment alignment in db.HashSetOfType<Alignment>(tx))
        {
            sources.Add(new AlignmentSource(alignment.Name, db, alignment.Id, toHost));
        }

        if (depth >= MaxXrefDepth)
        {
            return;
        }

        BlockTable bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (Oid id in modelSpace)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not BlockReference br)
            {
                continue;
            }

            if (tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) is not BlockTableRecord definition ||
                !definition.IsFromExternalReference)
            {
                continue;
            }

            // false = don't load an unloaded xref; an unresolved xref simply contributes nothing.
            Database? xrefDb = definition.GetXrefDatabase(false);
            if (xrefDb is null)
            {
                continue;
            }

            // Composed so a point in the nested database maps straight to host WCS in one go.
            Matrix3d childToHost = toHost * br.BlockTransform;

            using Transaction xrefTx = xrefDb.TransactionManager.StartTransaction();
            CollectFrom(xrefDb, xrefTx, childToHost, depth + 1, sources);
            xrefTx.Commit();
        }
    }

    /// <summary>
    /// Entries are listed by plain alignment name, and the dialog hands back a string — so two
    /// alignments sharing a name would be indistinguishable. Happens when the same name exists in
    /// both the host drawing and an xref, or when one xref is attached twice; suffix the later ones
    /// so every entry resolves to exactly one alignment.
    /// </summary>
    private static void MakeDisplayNamesUnique(List<AlignmentSource> sources)
    {
        HashSet<string> taken = [];
        for (int i = 0; i < sources.Count; i++)
        {
            string name = sources[i].DisplayName;
            if (taken.Add(name))
            {
                continue;
            }

            int suffix = 2;
            string candidate;
            do
            {
                candidate = $"{name} ({suffix++})";
            }
            while (!taken.Add(candidate));

            sources[i] = sources[i] with { DisplayName = candidate };
        }
    }
}
