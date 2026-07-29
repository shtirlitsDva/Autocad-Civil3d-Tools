using System.IO;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using static IntersectUtilities.Utils;
using static IntersectUtilities.UtilsCommon.Utils;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// Writes the chosen component block into the FV_Fremtid plan drawing. FV_Fremtid is never xrefed
/// into the længdeprofil drawing, so it is reached one of two ways: if it is already open in this
/// AutoCAD session the live document is edited directly, otherwise the file is side-loaded and saved.
/// Nothing here prompts — every interactive decision is made before this runs, because an editor
/// prompt under an open side-database transaction is a hazard.
/// </summary>
internal static class FremtidBlockWriter
{
    internal const string SymbolerPath = @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg";
    internal const string ComponentLayerName = "0-KOMPONENT";

    /// <summary>
    /// Imports the block definition if needed, inserts a reference at the plan point, sizes it from
    /// the pipe it lands on, and stamps BelongsToAlignment.
    /// </summary>
    /// <param name="localDb">
    /// The active længdeprofil drawing — it owns the Alignment the size array is built against.
    /// </param>
    internal static bool TryPlace(
        string fremtidPath,
        Database localDb,
        IReadOnlyList<LongitudinalBlockPick> picks,
        string blockName,
        out string message)
    {
        // A drawing open in AutoCAD holds its file locked, so side-loading and saving over it cannot
        // work. When it is open in *this* session we can edit the live document instead — which is
        // also the safer outcome, since saving a side copy would be silently overwritten by whatever
        // the open session saves later.
        Document? openDocument = FindOpenDocument(fremtidPath);

        return openDocument is null
            ? PlaceInSideDatabase(fremtidPath, localDb, picks, blockName, out message)
            : PlaceInOpenDocument(openDocument, localDb, picks, blockName, out message);
    }

    /// <summary>
    /// The FV_Fremtid document open in this AutoCAD session, or null. Matched on the database's
    /// filename, which is the full path for a saved drawing.
    /// </summary>
    private static Document? FindOpenDocument(string fremtidPath)
    {
        foreach (Document document in AcadApp.DocumentManager)
        {
            string? filename = document.Database?.Filename;
            if (!string.IsNullOrEmpty(filename) &&
                string.Equals(filename, fremtidPath, StringComparison.OrdinalIgnoreCase))
            {
                return document;
            }
        }

        return null;
    }

    /// <summary>
    /// Edits FV_Fremtid in place while it is open in this session. The drawing is left dirty on
    /// purpose — saving another document from under the user is worse than telling them to save.
    /// </summary>
    private static bool PlaceInOpenDocument(
        Document document,
        Database localDb,
        IReadOnlyList<LongitudinalBlockPick> picks,
        string blockName,
        out string message)
    {
        Database db = document.Database;

        try
        {
            // Required because we are writing to a document other than the one running the command.
            using DocumentLock documentLock = document.LockDocument();

            PropertySetManager.UpdatePropertySetDefinition(db, PSetDefs.DefinedSets.DriPipelineData);

            List<string> reports;
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                reports = WriteAll(db, tx, localDb, picks, blockName);
                tx.Commit();
            }

            message =
                $"{Describe(blockName, picks, reports)}\n" +
                "Skrevet direkte i den ÅBNE FV_Fremtid — husk at gemme den.";
            return true;
        }
        catch (System.Exception exception)
        {
            // Deliberately NOT AbortGracefully: that would dispose the Database of a live document.
            prdDbg(exception);
            message = $"ADVARSEL: blokken blev IKKE placeret i FV_Fremtid. {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Side-loads FV_Fremtid, writes, and saves. Opened with the FileShare overload rather than via
    /// DataManager.Fremtid(), which uses FileOpenMode.OpenForReadAndAllShare — a read-only open whose
    /// SaveAs fails with eFilerError.
    /// </summary>
    private static bool PlaceInSideDatabase(
        string fremtidPath,
        Database localDb,
        IReadOnlyList<LongitudinalBlockPick> picks,
        string blockName,
        out string message)
    {
        // Deliberately not a `using` declaration: AbortGracefully owns disposal on the failure path,
        // and combining the two double-disposes on every path.
        Database fremDb = new(false, true);
        List<string> reports;

        try
        {
            fremDb.ReadDwgFile(fremtidPath, FileShare.ReadWrite, false, "");

            // Must run outside a transaction — UpdatePropertySetDefinition no-ops when one is open.
            PropertySetManager.UpdatePropertySetDefinition(fremDb, PSetDefs.DefinedSets.DriPipelineData);

            using (Transaction fremTx = fremDb.TransactionManager.StartTransaction())
            {
                // Every placement goes into ONE transaction and ONE save — on a network drive a save
                // per click would dominate the run time.
                reports = WriteAll(fremDb, fremTx, localDb, picks, blockName);
                fremTx.Commit();
            }

            // Release the handle on the source DWG before saving; SaveAs with bBakAndRename renames
            // the original to .bak, which cannot happen while the read handle is still open.
            fremDb.CloseInput(true);
            fremDb.SaveAs(fremtidPath, true, DwgVersion.Current, fremDb.SecurityParameters);
        }
        catch (System.Exception exception)
        {
            // The transaction's `using` block already aborted it, so AbortGracefully only disposes.
            AbortGracefully(exception, fremDb);
            message =
                $"ADVARSEL: blokken blev IKKE gemt i FV_Fremtid. {exception.Message}\n" +
                $"Er tegningen åben i en anden AutoCAD-session? Luk den, eller åbn den i DENNE " +
                $"session — så skriver kommandoen direkte i den åbne tegning.\n{fremtidPath}";
            return false;
        }

        fremDb.Dispose();
        message = Describe(blockName, picks, reports);
        return true;
    }

    /// <summary>
    /// Writes every placement in one transaction. The pipeline size array is cached per alignment:
    /// building it walks all FJV entities on that alignment, so re-deriving it for each click would
    /// be pure waste when placing several components along the same pipe.
    /// </summary>
    private static List<string> WriteAll(
        Database db,
        Transaction tx,
        Database localDb,
        IReadOnlyList<LongitudinalBlockPick> picks,
        string blockName)
    {
        Dictionary<string, IPipelineSizeArrayV2?> sizeArrays = [];
        List<string> reports = [];

        foreach (LongitudinalBlockPick pick in picks)
        {
            WriteBlock(db, tx, localDb, pick, blockName, sizeArrays, out string handle, out string sizeNote);
            reports.Add($"  st. {pick.Station:0.###} på {pick.AlignmentName} (handle {handle}) — {sizeNote}");
        }

        return reports;
    }

    /// <summary>
    /// The actual edit, identical whichever database it runs against. Requires an open transaction
    /// on <paramref name="db"/>.
    /// </summary>
    private static void WriteBlock(
        Database db,
        Transaction tx,
        Database localDb,
        LongitudinalBlockPick pick,
        string blockName,
        Dictionary<string, IPipelineSizeArrayV2?> sizeArrays,
        out string handle,
        out string sizeNote)
    {
        db.CheckOrCreateLayer(ComponentLayerName, 0);

        try
        {
            db.CheckOrImportBlockRecord(SymbolerPath, blockName);
        }
        catch (System.Exception exception)
        {
            throw new System.Exception(
                $"Blokken '{blockName}' står i FJV Dynamiske Komponenter.csv, " +
                $"men kunne ikke hentes fra Symboler.dwg: {exception.Message}");
        }

        BlockReference br = db.CreateBlockWithAttributes(blockName, pick.PlanPoint, pick.Rotation);
        br.Layer = ComponentLayerName;

        ApplySizeAtStation(db, tx, localDb, br, pick, sizeArrays, out sizeNote);

        PropertySetManager psmPipeline = new(db, PSetDefs.DefinedSets.DriPipelineData);
        PSetDefs.DriPipelineData pipelineDef = new();
        psmPipeline.WritePropertyString(br, pipelineDef.BelongsToAlignment, pick.AlignmentName);

        MoveToTop(db, tx, br.Id);

        handle = br.Handle.ToString();
    }

    /// <summary>
    /// Lifts the new block to the top of the draw order. FJV pipes are drawn with a large global
    /// width, so they render as a filled band that hides anything ordered beneath it — and a plan
    /// drawing that has had its draw order sorted will otherwise leave the freshly added block buried.
    /// </summary>
    private static void MoveToTop(Database db, Transaction tx, ObjectId entityId)
    {
        BlockTableRecord modelSpace = db.GetModelspaceForWrite();
        DrawOrderTable drawOrder = (DrawOrderTable)tx.GetObject(modelSpace.DrawOrderTableId, OpenMode.ForWrite);
        drawOrder.MoveToTop(new ObjectIdCollection { entityId });
    }

    private static string Describe(
        string blockName,
        IReadOnlyList<LongitudinalBlockPick> picks,
        List<string> reports) =>
        $"{blockName} placeret {picks.Count} gang(e) i FV_Fremtid:\n" +
        string.Join("\n", reports);

    /// <summary>
    /// Sizes the new block from the pipeline size array — the same construction CREATEOFFSETPROFILES
    /// uses — so the size is the one in force AT THE STATION, correct across size changes rather than
    /// merely whatever polyline happens to be nearest. A size that cannot be resolved is a warning,
    /// not a failure: the block stays placed at its defaults for the user to correct in plan.
    /// </summary>
    private static void ApplySizeAtStation(
        Database fremDb,
        Transaction fremTx,
        Database localDb,
        BlockReference br,
        LongitudinalBlockPick pick,
        Dictionary<string, IPipelineSizeArrayV2?> sizeArrays,
        out string note)
    {
        if (!sizeArrays.TryGetValue(pick.AlignmentName, out IPipelineSizeArrayV2? sizeArray))
        {
            sizeArray = BuildSizeArray(fremDb, fremTx, localDb, pick);
            sizeArrays[pick.AlignmentName] = sizeArray;
        }

        if (sizeArray is null)
        {
            note = $"BEMÆRK: ingen rør fundet for {pick.AlignmentName} — blokken har standardværdier.";
            return;
        }

        SizeEntryV2 size = sizeArray.GetSizeAtStation(pick.Station);

        if (size.DN == 0)
        {
            note = $"BEMÆRK: kunne ikke bestemme dimension ved st. {pick.Station:0.###} — standardværdier.";
            return;
        }

        // Frem/Retur are both "Enkelt" as far as a component block is concerned; same normalisation
        // as ComponentData.PipeType.
        PipeTypeEnum type = size.Type is PipeTypeEnum.Frem or PipeTypeEnum.Retur
            ? PipeTypeEnum.Enkelt
            : size.Type;

        // The dynamic parameter names differ per block, so they are read from the catalogue rather
        // than guessed — see TrySetCsvDrivenProperty.
        List<string> applied = [];
        if (TrySetCsvDrivenProperty(br, DynamicProperty.DN1, size.DN.ToString())) applied.Add($"DN{size.DN}");
        if (TrySetCsvDrivenProperty(br, DynamicProperty.System, type.ToString())) applied.Add(type.ToString());
        if (TrySetCsvDrivenProperty(br, DynamicProperty.Serie, size.Series.ToString())) applied.Add(size.Series.ToString());
        br.AttSync();

        note = applied.Count == 0
            ? $"BEMÆRK: {br.RealName()} har ingen dimensionsparametre at sætte " +
              $"(st. {pick.Station:0.###} er DN{size.DN} {size.System} {type} {size.Series})."
            : $"Sat fra dimensionsarray ved st. {pick.Station:0.###}: {string.Join(" ", applied)}.";
    }

    /// <summary>
    /// Builds the pipeline size array for one alignment, or null when the plan drawing holds no FJV
    /// entities for it. Entities come from FV_Fremtid, the alignment from the active længdeprofil
    /// drawing — the same cross-database pairing CREATEOFFSETPROFILES uses.
    /// </summary>
    private static IPipelineSizeArrayV2? BuildSizeArray(
        Database fremDb,
        Transaction fremTx,
        Database localDb,
        LongitudinalBlockPick pick)
    {
        PropertySetManager psmPipeline = new(fremDb, PSetDefs.DefinedSets.DriPipelineData);
        PSetDefs.DriPipelineData pipelineDef = new();

        HashSet<Entity> ents = fremDb.GetFjvEntities(fremTx)
            .Where(x => psmPipeline.FilterPropetyString(x, pipelineDef.BelongsToAlignment, pick.AlignmentName))
            .ToHashSet();

        if (ents.Count == 0)
        {
            return null;
        }

        using Transaction localTx = localDb.TransactionManager.StartTransaction();
        Alignment alignment = pick.AlignmentId.Go<Alignment>(localTx);
        IPipelineV2 pipeline = PipelineV2Factory.Create(ents, alignment);
        IPipelineSizeArrayV2 sizeArray = PipelineSizeArrayFactory.CreateSizeArray(pipeline);
        localTx.Commit();
        return sizeArray;
    }

    /// <summary>
    /// Sets a dynamic block parameter, resolving its NAME from FJV Dynamiske Komponenter.csv rather
    /// than assuming one. The catalogue cell for a column is either a literal, or <c>$Parameter</c>
    /// naming the dynamic property that holds the value, or <c>#Attribute</c> — which is exactly how
    /// <see cref="ComponentSchedule.ReadDynamicCsvProperty"/> reads it back. Blocks disagree on the
    /// names (DN vs DN1 vs Dim), so hard-coding them silently sizes nothing on most components.
    /// Returns false when this block has no settable parameter for the column.
    /// </summary>
    private static bool TrySetCsvDrivenProperty(BlockReference br, DynamicProperty property, string value)
    {
        // parseProperty: false gives the raw catalogue cell — the spec — instead of a resolved value.
        string spec = br.ReadDynamicCsvProperty(property, false);

        // Not a "$Parameter" reference: either a literal (fixed for this block) or a regex extraction
        // pattern. Neither is something we can write back.
        if (string.IsNullOrEmpty(spec) || !spec.StartsWith('$') || spec.Contains('{'))
        {
            return false;
        }

        string parameterName = spec[1..];

        try
        {
            SetDynBlockPropertyObject(br, parameterName, value);
            return true;
        }
        catch (System.Exception exception)
        {
            // A size the block cannot represent must not lose the placement.
            prdDbg($"Kunne ikke sætte {parameterName}={value} på {br.RealName()}: {exception.Message}");
            return false;
        }
    }
}
