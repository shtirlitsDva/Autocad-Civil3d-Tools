using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Reads a legacy FJV drawing into a <see cref="LegacyDrawing"/>: its
/// pipelines' traces, its branches, its end-to-end joins, its service
/// connections and its drawing-wide settings facts.
///
/// The drawing is opened from its FILE into a side database, never through the
/// xref: nothing of it is kept, and nothing is saved; the one transaction over
/// it is aborted.
///
/// Order matters. The traces and the parts' pipeline properties are read
/// FIRST, as the legacy drawing has them. Only then is the legacy connection
/// graph (DriGraph ConnectedEntities) rewritten, inside that transaction, as
/// GRAPHPOPULATE would (the file's own copy may be stale): the graph writer
/// also "repairs" a svanehals's missing BelongsToAlignment with its main, and
/// that may not change what the traces or the parts say.
/// </summary>
internal static class LegacyDrawingReader
{
    public static LegacyDrawing Read(string fjvPath)
    {
        using Database fjvDb = new Database(false, true);
        fjvDb.ReadDwgFile(fjvPath, FileOpenMode.OpenForReadAndAllShare, true, "");

        using Transaction tx = fjvDb.TransactionManager.StartTransaction();
        PropertySetHelper psh = new PropertySetHelper(fjvDb);
        //Database resident: used only in this method, while tx is open.
        LegacyPipelineGroups groups = LegacyPipelineGroups.Read(fjvDb, tx, psh);
        LegacyDrawing drawing = new LegacyDrawing(FjvLegacyPipelineReader.Read(groups, tx));
        try
        {
            List<LegacyComponent> parts = LegacyComponentReader.Read(fjvDb, tx, psh);

            Dictionary<string, List<Entity>> importedGroups = drawing.Traces.Traces
                .ToDictionary(t => t.Name, t => groups.ByName[t.Name], StringComparer.Ordinal);
            LegacySettingsReader.Read(parts, importedGroups, drawing.Settings);

            try
            {
                //Nested in tx, so the rewrite is thrown away with it.
                Intersect.ClearGraph(fjvDb);
                Intersect.PopulateGraph(fjvDb);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Forbindelsesgrafen for den gamle tegning kunne ikke skrives: {ex.Message}", ex);
            }

            LegacyBranchFinder.Read(parts, fjvDb, tx, psh, drawing);
            LegacyNetwork.Read(parts, importedGroups, drawing);

            tx.Abort();
            return drawing;
        }
        catch
        {
            drawing.Dispose();
            throw;
        }
    }
}
