using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using Autodesk.Civil.DatabaseServices;

using CSharpFunctionalExtensions;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.UtilsCommon.Enums;

using Norsyn.AecPropertySets;

using static IntersectUtilities.Utils;
using static IntersectUtilities.UtilsCommon.Utils;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Result = CSharpFunctionalExtensions.Result;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// Writes a job's component blocks into FV_Fremtid. It runs inside PLACELONGITUDINALBLOCKFV, so FV_Fremtid is the
/// active document, locked by its own command, and everything written is that command's one UNDO step. Nothing
/// here prompts — every interactive decision was made in the længdeprofil.
/// </summary>
internal static class FremtidBlockWriter
{
    internal const string SymbolerPath = @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg";
    internal const string ComponentLayerName = "0-KOMPONENT";

    /// <summary>
    /// What sizing did to a block: the report's size note, and, when the block could not be sized, why its pipe is
    /// not cut.
    /// </summary>
    private sealed record Sizing(string Note, Maybe<string> NotCutBecause)
    {
        internal static Sizing Sized(string note) => new(note, Maybe<string>.None);

        internal static Sizing Unsized(string note, string reason) => new(note, Maybe.From(reason));
    }

    /// <summary>A block written by the first transaction, with what sizing did, waiting for the trim.</summary>
    private sealed record Placed(ObjectId BlockId, string Handle, LongitudinalBlockPick Pick, Sizing Sizing);

    /// <summary>
    /// Places and sizes every block of the job in one transaction and commits it; regenerates FV_Fremtid, so AutoCAD
    /// brings the sized dynamic blocks up to date; then, in a second transaction, trims the pipe at each sized
    /// block's ports, in pick order, so each trim sees the previous one. Returns the report. A trim the drawing
    /// refuses is a report line, not a failure; a REGEN AutoCAD refuses leaves every pipe untrimmed, with the
    /// reason. Anything thrown here is a defect: it leaves the transaction it hits uncommitted, keeps what was
    /// written before (the property-set definition update, and after the first commit the imported block
    /// definition, the layer, the property sets the size lookup attached and the placed blocks), and passes to the
    /// command's guard. One UNDO in FV_Fremtid removes all of it.
    /// </summary>
    internal static string Write(Document fremtid, FremtidJob job)
    {
        Database db = fremtid.Database;

        // Must run outside a transaction — UpdatePropertySetDefinition no-ops when one is open.
        PropertySetManager.UpdatePropertySetDefinition(db, PSetDefs.DefinedSets.DriPipelineData);

        List<Placed> placed;
        using (Transaction placeTx = db.TransactionManager.StartTransaction())
        {
            placed = PlaceAll(db, placeTx, job);
            placeTx.Commit();
        }

        // A commit and FlushGraphics left a sized dynamic block at its default geometry (live round 1: a DN125
        // BØJN KDLR VERT kept its 0.963 m default port spacing); a REGEN brought it up to date. Run as a nested
        // command (Editor.Command), with no transaction open, it is also a command boundary, and it stays inside
        // this command's one UNDO step. Without it nothing is trimmed: a cut at unsized ports is worse than none.
        UnitResult<AecError> regenerated = AutoCadCall.Do("REGEN", () => fremtid.Editor.Command("_.REGEN"));

        List<Maybe<string>> notes = [];
        List<string> lines = regenerated.Match(
            () => TrimAll(fremtid, db, placed, notes),
            error => placed.Select(block => Line(block, $"rør ikke trimmet: {error.Message}")).ToList());

        notes.Add(Redraw("FlushGraphics", () => fremtid.TransactionManager.FlushGraphics()));
        notes.Add(Redraw("UpdateScreen", () => fremtid.Editor.UpdateScreen()));

        return string.Join("\n", new[] { $"{job.BlockName} placeret {job.Picks.Count} gang(e) i FV_Fremtid:" }
            .Concat(lines)
            .Concat(notes.Choose().Distinct())
            .Append("Husk at gemme FV_Fremtid. Én UNDO i FV_Fremtid fjerner hele placeringen."));
    }

    /// <summary>
    /// The second transaction: trims at every placed block, in pick order, and queues the changed entities for the
    /// screen; a refused queue becomes a note.
    /// </summary>
    private static List<string> TrimAll(Document fremtid, Database db, IReadOnlyList<Placed> placed, List<Maybe<string>> notes)
    {
        using Transaction trimTx = db.TransactionManager.StartTransaction();
        GraphLinks graph = new(db, trimTx);
        List<string> lines = placed.Select(block => TrimAt(db, trimTx, block, graph)).ToList();
        notes.Add(Redraw("QueueForGraphicsFlush", () => fremtid.TransactionManager.QueueForGraphicsFlush()));
        trimTx.Commit();
        return lines;
    }

    /// <summary>A screen update AutoCAD refused, as a report note; the write itself is not affected.</summary>
    private static Maybe<string> Redraw(string what, Action call) =>
        AutoCadCall.Do(what, call).Match(
            () => Maybe<string>.None,
            error => Maybe.From($"BEMÆRK: skærmen er måske ikke opdateret ({error.Message}) — brug REGEN."));

    /// <summary>
    /// Inserts and sizes every block and stamps BelongsToAlignment. The pipeline size array is cached per
    /// alignment: building it walks all FJV entities on that alignment, so re-deriving it for each click would be
    /// pure waste when placing several components along the same pipe.
    /// </summary>
    private static List<Placed> PlaceAll(Database db, Transaction tx, FremtidJob job)
    {
        Dictionary<string, IPipelineSizeArrayV2?> sizeArrays = [];
        return job.Picks
            .Select(pick => Place(db, tx, job.Profile.Database, pick, job.BlockName, sizeArrays))
            .ToList();
    }

    private static Placed Place(
        Database db,
        Transaction tx,
        Database localDb,
        LongitudinalBlockPick pick,
        string blockName,
        Dictionary<string, IPipelineSizeArrayV2?> sizeArrays)
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

        Sizing sizing = SizeAtStation(db, tx, localDb, br, pick, sizeArrays);

        PropertySetManager psmPipeline = new(db, PSetDefs.DefinedSets.DriPipelineData);
        PSetDefs.DriPipelineData pipelineDef = new();
        psmPipeline.WritePropertyString(br, pipelineDef.BelongsToAlignment, pick.AlignmentName);

        return new Placed(br.ObjectId, br.Handle.ToString(), pick, sizing);
    }

    /// <summary>
    /// Trims the pipe at the ports of a block placed by the committed first transaction and brought up to date by
    /// the REGEN, opened afresh here, so the ports are those of the sized block; returns the placement's report line.
    /// A block that could not be sized is not cut: its ports are the default geometry's.
    /// </summary>
    private static string TrimAt(Database db, Transaction tx, Placed placed, GraphLinks graph)
    {
        // Forced open, as the block was writable while the first transaction made it, even on a locked layer.
        BlockReference br = (BlockReference)tx.GetObject(placed.BlockId, OpenMode.ForWrite, false, true);

        string trimNote = placed.Sizing.NotCutBecause.Match(
            reason => $"rør ikke trimmet: {reason}",
            () => PipeTrimmer.Trim(db, tx, br, placed.Pick.AlignmentName, graph).Match(
                trimmed => $"rør trimmet: {trimmed}",
                error => $"rør ikke trimmet: {error.Message}"));

        // After the trim, or a new pipe piece would be drawn over the block.
        MoveToTop(db, tx, br.Id);

        return Line(placed, trimNote);
    }

    private static string Line(Placed placed, string trimNote) =>
        $"  st. {placed.Pick.Station:0.###} på {placed.Pick.AlignmentName} (handle {placed.Handle}) — " +
        $"{placed.Sizing.Note} — {trimNote}";

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

    /// <summary>
    /// Sizes the new block from the pipeline size array — the same construction CREATEOFFSETPROFILES
    /// uses — so the size is the one in force AT THE STATION, correct across size changes rather than
    /// merely whatever polyline happens to be nearest. A block that could not be sized stays placed at
    /// its defaults for the user to correct in plan, and is not cut: its default ports are not the
    /// sized block's. A block without size parameters (fixed geometry) is sized as it is.
    /// </summary>
    private static Sizing SizeAtStation(
        Database fremDb,
        Transaction fremTx,
        Database localDb,
        BlockReference br,
        LongitudinalBlockPick pick,
        Dictionary<string, IPipelineSizeArrayV2?> sizeArrays)
    {
        // A definition already present in FV_Fremtid is never refreshed — CheckOrImportBlockRecord
        // imports only when the name is ABSENT — so a drawing carrying an older VERSION of the block
        // resolves every catalogue lookup to "" and lands on its defaults. Say that, rather than the
        // generic "no parameters to set" note it would otherwise produce.
        if (!CatalogueHasRowForVersion(br, out string blockVersion))
        {
            return Sizing.Unsized(
                $"ADVARSEL: blokdefinitionen '{br.RealName()}' i FV_Fremtid har VERSION " +
                $"'{blockVersion}', som ikke findes i FJV Dynamiske Komponenter.csv — INGEN " +
                $"dimensioner er sat. Opdatér blokken i FV_Fremtid fra Symboler.dwg.",
                $"blokken kunne ikke dimensioneres (VERSION '{blockVersion}' findes ikke i kataloget)");
        }

        if (!sizeArrays.TryGetValue(pick.AlignmentName, out IPipelineSizeArrayV2? sizeArray))
        {
            sizeArray = BuildSizeArray(fremDb, fremTx, localDb, pick);
            sizeArrays[pick.AlignmentName] = sizeArray;
        }

        if (sizeArray is null)
        {
            return Sizing.Unsized(
                $"BEMÆRK: ingen rør fundet for {pick.AlignmentName} — blokken har standardværdier.",
                $"blokken kunne ikke dimensioneres (ingen rør fundet for {pick.AlignmentName})");
        }

        SizeEntryV2 size = sizeArray.GetSizeAtStation(pick.Station);

        if (size.DN == 0)
        {
            return Sizing.Unsized(
                $"BEMÆRK: kunne ikke bestemme dimension ved st. {pick.Station:0.###} — standardværdier.",
                $"blokken kunne ikke dimensioneres (ingen dimension ved st. {pick.Station:0.###})");
        }

        // Frem/Retur are both "Enkelt" as far as a component block is concerned; same normalisation
        // as ComponentData.PipeType.
        PipeTypeEnum type = size.Type is PipeTypeEnum.Frem or PipeTypeEnum.Retur
            ? PipeTypeEnum.Enkelt
            : size.Type;

        // The dynamic parameter names differ per block, so they are read from the catalogue rather
        // than guessed — see SetCsvDrivenProperty. All three are tried, in this order.
        List<Result<Maybe<string>, AecError>> results =
        [
            SetCsvDrivenProperty(br, DynamicProperty.DN1, size.DN.ToString(), $"DN{size.DN}"),
            SetCsvDrivenProperty(br, DynamicProperty.System, type.ToString(), type.ToString()),
            SetCsvDrivenProperty(br, DynamicProperty.Serie, size.Series.ToString(), size.Series.ToString()),
        ];
        br.AttSync();

        List<string> applied = results.Select(result => result.Match(label => label, _ => Maybe<string>.None)).Choose().ToList();
        List<string> failed = results.Select(result => result.Match(_ => Maybe<string>.None, error => Maybe.From(error.Message))).Choose().ToList();

        if (failed.Count > 0)
        {
            string reasons = string.Join("; ", failed);
            return Sizing.Unsized(
                $"ADVARSEL: dimensioneringen ved st. {pick.Station:0.###} fejlede ({reasons})" +
                (applied.Count > 0 ? $"; sat: {string.Join(" ", applied)}." : "."),
                $"blokken kunne ikke dimensioneres ({reasons})");
        }

        return Sizing.Sized(applied.Count == 0
            ? $"BEMÆRK: {br.RealName()} har ingen dimensionsparametre at sætte " +
              $"(st. {pick.Station:0.###} er DN{size.DN} {size.System} {type} {size.Series})."
            : $"Sat fra dimensionsarray ved st. {pick.Station:0.###}: {string.Join(" ", applied)}.");
    }

    /// <summary>
    /// Builds the pipeline size array for one alignment, or null when the plan drawing holds no FJV
    /// entities for it. Entities come from FV_Fremtid, the alignment from the længdeprofil drawing,
    /// read without a lock — the same cross-database pairing CREATEOFFSETPROFILES uses. It runs here,
    /// inside FV_Fremtid's command, because reading the entities attaches property sets to them.
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
    /// True when the catalogue holds a row matching this reference's Navn AND its VERSION attribute
    /// — the exact pair <see cref="ComponentSchedule.ReadDynamicCsvProperty"/> matches on. Mirrors
    /// that comparison (ordinal, both columns) so this check cannot disagree with the lookup it
    /// guards.
    /// </summary>
    private static bool CatalogueHasRowForVersion(BlockReference br, out string blockVersion)
    {
        // Copied to a local because an out parameter cannot be captured by the lambda below.
        string version = br.GetAttributeStringValue("VERSION") ?? string.Empty;
        string name = br.RealName();
        blockVersion = version;

        return Csv.FjvDynamicComponents.Rows.Any(row =>
            CsvDataSource.Col(row, FjvDynamicComponents.Columns.Navn) == name &&
            CsvDataSource.Col(row, FjvDynamicComponents.Columns.Version) == version);
    }

    /// <summary>
    /// Sets a dynamic block parameter, resolving its NAME from FJV Dynamiske Komponenter.csv rather
    /// than assuming one. The catalogue cell for a column is either a literal, or <c>$Parameter</c>
    /// naming the dynamic property that holds the value, or <c>#Attribute</c> — which is exactly how
    /// <see cref="ComponentSchedule.ReadDynamicCsvProperty"/> reads it back. Blocks disagree on the
    /// names (DN vs DN1 vs Dim), so hard-coding them silently sizes nothing on most components.
    /// Success with <paramref name="label"/> when set; success with None when the catalogue names no
    /// parameter for the column (fixed for this block); a failure when the block has no such parameter
    /// or AutoCAD refuses the value — the size was not applied. A failure never loses the placement.
    /// </summary>
    private static Result<Maybe<string>, AecError> SetCsvDrivenProperty(
        BlockReference br, DynamicProperty property, string value, string label)
    {
        // parseProperty: false gives the raw catalogue cell — the spec — instead of a resolved value.
        string spec = br.ReadDynamicCsvProperty(property, false);

        // Not a "$Parameter" reference: either a literal (fixed for this block) or a regex extraction
        // pattern. Neither is something we can write back.
        if (string.IsNullOrEmpty(spec) || !spec.StartsWith('$') || spec.Contains('{'))
        {
            return Result.Success<Maybe<string>, AecError>(Maybe<string>.None);
        }

        string parameterName = spec[1..];

        return DynamicPropertyNamed(br, parameterName)
            .ToResult(AecError.Drawing($"{br.RealName()} har ingen parameter {parameterName}"))
            .Bind(parameter => AutoCadCall
                .Do($"sæt {parameterName}={value}", () => parameter.Value = value)
                .Map(() => Maybe.From(label)));
    }

    private static Maybe<DynamicBlockReferenceProperty> DynamicPropertyNamed(BlockReference br, string name) =>
        br.IsDynamicBlock
            ? br.DynamicBlockReferencePropertyCollection
                .Cast<DynamicBlockReferenceProperty>()
                .TryFirst(parameter => parameter.PropertyName == name)
            : Maybe<DynamicBlockReferenceProperty>.None;
}
