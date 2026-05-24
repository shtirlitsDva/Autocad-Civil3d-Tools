using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using IntersectUtilities.MPE.PipePlan;
using IntersectUtilities.UtilsCommon.Enums;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>PPSPLIT</command>
    /// <summary>Splits a metadata-enabled PipePlan object into two new independent PipePlan objects. The split must resolve to a valid straight portion of the baked polyline; arc regions and invalid split positions are rejected.</summary>
    /// <category>PipePlan</category>
    [CommandMethod("PPSPLIT")]
    public void PipePlanSplit()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteSplit(document);
        }
        catch (System.Exception exception)
        {
            HandleCommandException(document, "PPSPLIT", exception);
        }
    }

    /// <command>PPEDIT</command>
    /// <summary>Edits an existing metadata-enabled PipePlan object by moving control handles or segment handles while preserving PipePlan constraints. The command previews each move live, rejects infeasible edits, and finishes when Enter is pressed at the handle prompt.</summary>
    /// <category>PipePlan</category>
    [CommandMethod("PPEDIT")]
    public void PipePlanEdit()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteEdit(document);
        }
        catch (System.Exception exception)
        {
            HandleCommandException(document, "PPEDIT", exception);
        }
    }

    /// <command>PPSETTINGS</command>
    /// <summary>Shows the PipePlan settings palette so the per-DN bending radius (ProjekteringsRadius) and the straight-snap tolerance can be edited. Overrides are saved to the active drawing.</summary>
    /// <category>PipePlan</category>
    [CommandMethod("PPSETTINGS")]
    public void PipePlanSettings()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteSettings(document);
        }
        catch (System.Exception exception)
        {
            HandleCommandException(document, "PPSETTINGS", exception);
        }
    }

    /// <command>PPDRAW</command>
    /// <summary>Starts a new PipePlan draft or continues an existing metadata-enabled PipePlan object. Requires NSPalette to be loaded; the active FJV layer determines the pipe system, type, and DN. The bending radius comes from PPSETTINGS (per-drawing override or built-in default).</summary>
    /// <category>PipePlan</category>
    [CommandMethod("PPDRAW")]
    public void PipePlan()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteDraw(document);
        }
        catch (System.Exception exception)
        {
            HandleCommandException(document, "PPDRAW", exception);
        }
    }

    /// <command>PPCONVERT</command>
    /// <summary>Converts an existing polyline on a recognised FJV layer into a metadata-enabled PipePlan object by reverse-engineering its control points and bend radii. Sharp interior corners are filleted at the project minimum bending radius.</summary>
    /// <category>PipePlan</category>
    [CommandMethod("PPCONVERT")]
    public void PipePlanConvert()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteConvert(document);
        }
        catch (System.Exception exception)
        {
            HandleCommandException(document, "PPCONVERT", exception);
        }
    }

    private static Document? GetActiveDocument()
    {
        return Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
    }

    private static void ExecuteSplit(Document document)
    {
        if (!PipePlanSplitService.TrySplit(document, out string message))
        {
            ReportMessage(document, message, PipePlanStatusKind.Warning);
            return;
        }

        ReportMessage(document, message, PipePlanStatusKind.Ok);
    }

    private static void ExecuteEdit(Document document)
    {
        if (!TryCreateEditSession(document, out PipePlanEditSession? session))
        {
            return;
        }

        PipePlanEditSession activeSession = session;
        using (activeSession)
        {
            RunEditLoop(document, activeSession);
        }
    }

    private static bool TryCreateEditSession(Document document, [NotNullWhen(true)] out PipePlanEditSession? session)
    {
        session = null;
        if (!PipePlanEditSession.TryCreate(document, PipePlanRuntime.StateFor(document), out session, out string errorMessage) || session is null)
        {
            ReportMessage(document, errorMessage, PipePlanStatusKind.Warning);
            return false;
        }

        return true;
    }

    private enum VertexRadiusEditOutcome { Locked, Cancelled }

    private static void RunEditLoop(Document document, PipePlanEditSession session)
    {
        Editor editor = document.Editor;
        PipePlanRuntime.StateFor(document).SetStatus(
            $"Redigerer {session.SizeLabel} (R={session.RadiusDisplay}). Vælg håndtag, eller Enter for at afslutte.",
            PipePlanStatusKind.Info);

        while (true)
        {
            session.ShowHandles();

            PromptPointResult pickResult = PromptForEditHandle(editor);
            if (pickResult.Status == PromptStatus.None)
            {
                session.ClearVisuals();
                PipePlanRuntime.StateFor(document).SetStatus("PPEDIT afsluttet.", PipePlanStatusKind.Info);
                return;
            }

            if (pickResult.Status != PromptStatus.OK)
            {
                session.ClearVisuals();
                PipePlanRuntime.StateFor(document).SetStatus("PPEDIT annulleret.", PipePlanStatusKind.Info);
                return;
            }

            if (!TryResolveEditHandle(document, session, editor, pickResult.Value, out PipePlanEditHandle? handle) || handle is null)
            {
                continue;
            }

            if (!RunHandleEditLoop(document, session, handle))
            {
                return;
            }
        }
    }

    private static bool RunHandleEditLoop(Document document, PipePlanEditSession session, PipePlanEditHandle handle)
    {
        while (true)
        {
            session.ClearVisuals();
            PromptPointResult dragResult = PromptForEditMove(document, session, handle);

            if (dragResult.Status == PromptStatus.Keyword &&
                string.Equals(dragResult.StringResult, "Radius", StringComparison.OrdinalIgnoreCase))
            {
                VertexRadiusEditOutcome outcome = HandleVertexRadiusEdit(document, session, handle);
                if (outcome == VertexRadiusEditOutcome.Locked)
                {
                    continue;
                }

                session.ClearPendingRadius();
                PipePlanRuntime.StateFor(document).ClearPreview();
                return true;
            }

            if (dragResult.Status == PromptStatus.None)
            {
                if (!TryCommitFromLastDragPosition(document, session, handle))
                {
                    session.ClearPendingRadius();
                    PipePlanRuntime.StateFor(document).ClearPreview();
                    PipePlanRuntime.StateFor(document).SetStatus("Annulleret. Vælg et andet håndtag.", PipePlanStatusKind.Info);
                }
                return true;
            }

            if (dragResult.Status != PromptStatus.OK)
            {
                session.ClearPendingRadius();
                session.ClearVisuals();
                PipePlanRuntime.StateFor(document).SetStatus("PPEDIT annulleret.", PipePlanStatusKind.Info);
                return false;
            }

            ApplyEditCandidate(document, session, handle, dragResult.Value);
            return true;
        }
    }

    private static bool TryCommitFromLastDragPosition(
        Document document,
        PipePlanEditSession session,
        PipePlanEditHandle handle)
    {
        if (!session.TryGetPendingRadius(out _, out _))
        {
            return false;
        }

        Point3d lastPoint = PipePlanRuntime.StateFor(document).LastEditDragPoint ?? handle.GripPoint;
        ApplyEditCandidate(document, session, handle, lastPoint);
        return true;
    }

    private static VertexRadiusEditOutcome HandleVertexRadiusEdit(Document document, PipePlanEditSession session, PipePlanEditHandle handle)
    {
        if (handle.Kind != PipePlanEditHandleKind.Vertex)
        {
            return VertexRadiusEditOutcome.Cancelled;
        }

        double current = session.TryGetPendingRadius(out _, out double existingPending)
            ? existingPending
            : handle.Index < session.CurrentBendRadii.Count
                ? session.CurrentBendRadii[handle.Index]
                : 0.0;

        Point3d dragPosition = PipePlanRuntime.StateFor(document).LastEditDragPoint ?? handle.GripPoint;

        Editor editor = document.Editor;
        double? pendingRadius = null;

        while (true)
        {
            string prompt = pendingRadius.HasValue
                ? $"\nPreviewing radius {pendingRadius.Value}. Enter to lock, another value to preview, or [Default]: "
                : current > 0.0
                    ? $"\nNew radius for vertex <{current}> or [Default]: "
                    : "\nNew radius for vertex or [Default]: ";

            PromptDoubleOptions opts = new(prompt)
            {
                AllowNegative = false,
                AllowZero = false,
                AllowNone = true
            };
            opts.Keywords.Add("Default");

            PromptDoubleResult res = editor.GetDouble(opts);

            if (res.Status == PromptStatus.Keyword &&
                string.Equals(res.StringResult, "Default", StringComparison.OrdinalIgnoreCase))
            {
                if (TryPreviewDefaultVertexRadius(document, session, handle, dragPosition, out double defaultRadius))
                {
                    pendingRadius = defaultRadius;
                }
                continue;
            }

            if (res.Status == PromptStatus.None)
            {
                if (!pendingRadius.HasValue)
                {
                    PipePlanRuntime.StateFor(document).ClearPreview();
                    PipePlanRuntime.StateFor(document).SetStatus("Radius uændret.", PipePlanStatusKind.Info);
                    return VertexRadiusEditOutcome.Cancelled;
                }

                session.SetPendingRadius(handle.Index, pendingRadius.Value);
                PipePlanRuntime.StateFor(document).SetStatus(
                    $"Radius {pendingRadius.Value} låst. Flyt hjørnet, derefter Enter eller klik.",
                    PipePlanStatusKind.Info);
                return VertexRadiusEditOutcome.Locked;
            }

            if (res.Status != PromptStatus.OK)
            {
                PipePlanRuntime.StateFor(document).ClearPreview();
                PipePlanRuntime.StateFor(document).SetStatus("Radius-redigering annulleret.", PipePlanStatusKind.Info);
                return VertexRadiusEditOutcome.Cancelled;
            }

            if (!session.TryAnalyzeVertexState(handle.Index, dragPosition, res.Value, out PipePlanAnalysis previewAnalysis, out string previewError))
            {
                ReportEditorMessage(editor, $"Radius afvist: {previewError}");
                PipePlanRuntime.StateFor(document).SetStatus(previewError, PipePlanStatusKind.Error);
                continue;
            }

            PipePlanRuntime.StateFor(document).ShowPreview(previewAnalysis);
            pendingRadius = res.Value;
            PipePlanRuntime.StateFor(document).SetStatus($"Forhåndsviser radius {res.Value}.", PipePlanStatusKind.Info);
        }
    }

    private static bool TryPreviewDefaultVertexRadius(
        Document document,
        PipePlanEditSession session,
        PipePlanEditHandle handle,
        Point3d dragPosition,
        out double defaultRadius)
    {
        defaultRadius = 0.0;
        PipePlanActiveContext? ctx = PipePlanRuntime.StateFor(document).ActiveContext;
        if (ctx is null)
        {
            PipePlanRuntime.StateFor(document).SetStatus("Ingen aktiv dimension — vælg i NSPalette.", PipePlanStatusKind.Warning);
            return false;
        }

        if (!PipePlanRadiusStore.TryGet(document.Database, ctx.System, ctx.Type, ctx.Dn, out double resolved) || resolved <= 0.0)
        {
            PipePlanRuntime.StateFor(document).SetStatus($"Ingen standard-radius for {ctx.System} {ctx.Type} DN{ctx.Dn}.", PipePlanStatusKind.Warning);
            return false;
        }

        if (!session.TryAnalyzeVertexState(handle.Index, dragPosition, resolved, out PipePlanAnalysis analysis, out string error))
        {
            ReportEditorMessage(document.Editor, $"Standard-radius afvist: {error}");
            PipePlanRuntime.StateFor(document).SetStatus(error, PipePlanStatusKind.Error);
            return false;
        }

        PipePlanRuntime.StateFor(document).ShowPreview(analysis);
        PipePlanRuntime.StateFor(document).SetStatus($"Forhåndsviser standard-radius {resolved}.", PipePlanStatusKind.Info);
        defaultRadius = resolved;
        return true;
    }

    private static PromptPointResult PromptForEditHandle(Editor editor)
    {
        PromptPointOptions pickOptions = new("\nPick a PipePlan control handle or press Enter to finish: ")
        {
            AllowNone = true
        };

        return editor.GetPoint(pickOptions);
    }

    private static bool TryResolveEditHandle(
        Document document,
        PipePlanEditSession session,
        Editor editor,
        Point3d pickedPoint,
        out PipePlanEditHandle? handle)
    {
        handle = null;
        if (!session.TryResolveHandle(pickedPoint, out handle, out string handleMessage) || handle is null)
        {
            ReportEditorMessage(editor, handleMessage);
            PipePlanRuntime.StateFor(document).SetStatus(handleMessage, PipePlanStatusKind.Warning);
            return false;
        }

        return true;
    }

    private static PromptPointResult PromptForEditMove(
        Document document,
        PipePlanEditSession session,
        PipePlanEditHandle handle)
    {
        PipePlanRuntime.StateFor(document).LastEditDragPoint = handle.GripPoint;
        using PipePlanEditTracker tracker = new(document, PipePlanRuntime.StateFor(document), session, handle);
        string prompt = handle.Kind == PipePlanEditHandleKind.Vertex
            ? "\nMove the selected handle, [Radius] to change bend radius, or press Enter to cancel: "
            : "\nMove the selected handle or press Enter to cancel: ";

        PromptPointOptions dragOptions = new(prompt)
        {
            BasePoint = handle.GripPoint,
            UseBasePoint = true,
            AllowNone = true
        };

        if (handle.Kind == PipePlanEditHandleKind.Vertex)
        {
            dragOptions.Keywords.Add("Radius");
        }

        return document.Editor.GetPoint(dragOptions);
    }

    private static void ApplyEditCandidate(
        Document document,
        PipePlanEditSession session,
        PipePlanEditHandle handle,
        Point3d candidatePoint)
    {
        PipePlanEditCandidate candidate = session.BuildCandidate(handle, candidatePoint);
        if (!candidate.Analysis.IsFeasible)
        {
            PipePlanRuntime.StateFor(document).ClearPreview();
            ReportEditorMessage(document.Editor, $"Redigering afvist: {candidate.Analysis.Message}");
            PipePlanRuntime.StateFor(document).SetStatus(candidate.Analysis.Message, PipePlanStatusKind.Error);
            return;
        }

        session.Commit(candidate);
        PipePlanRuntime.StateFor(document).ClearPreview();
        PipePlanRuntime.StateFor(document).SetStatus("Anvendt. Vælg et andet håndtag, eller Enter.", PipePlanStatusKind.Ok);
    }

    private static void ExecuteSettings(Document document)
    {
        PipePlanState state = PipePlanRuntime.StateFor(document);
        PipePlanRuntime.Palette.RebindTo(state);
        PipePlanRuntime.Palette.Show();
        state.SetStatus("Rediger per-DN radier og klik Save.", PipePlanStatusKind.Info);
    }

    private static void ExecuteDraw(Document document)
    {
        if (!NSPaletteAdapter.IsLoaded)
        {
            ReportMessage(document, "Indlæs NSPalette først.", PipePlanStatusKind.Warning);
            return;
        }

        PipePlanRuntime.StateFor(document).ResetDraft(clearStatus: false);

        if (!TryInitializeDraw(document, out string initializationError))
        {
            ReportMessage(document, initializationError, PipePlanStatusKind.Warning);
            return;
        }

        RunDrawLoop(document);
    }

    private static void ExecuteConvert(Document document)
    {
        Editor editor = document.Editor;

        PromptEntityOptions options = new("\nSelect a polyline on an FJV layer to convert: ");
        options.SetRejectMessage("\nOnly polylines are supported.");
        options.AddAllowedClass(typeof(Polyline), exactMatch: false);

        PromptEntityResult pick = editor.GetEntity(options);
        if (pick.Status != PromptStatus.OK)
        {
            ReportMessage(document, "PPCONVERT annulleret.", PipePlanStatusKind.Info);
            return;
        }

        using DocumentLock documentLock = document.LockDocument();
        using PipePlanSharpCornerMarkerManager markers = new();
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            Polyline source = (Polyline)transaction.GetObject(pick.ObjectId, OpenMode.ForRead);

            string layerName = source.Layer;
            PipeSystemEnum system = PipeScheduleV2.PipeScheduleV2.GetPipeSystem(layerName);
            PipeTypeEnum type = PipeScheduleV2.PipeScheduleV2.GetPipeType(layerName);
            int dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(layerName);

            if (system == PipeSystemEnum.Ukendt || type == PipeTypeEnum.Ukendt || dn <= 0)
            {
                ReportMessage(document, $"Polylinjen er ikke på et FJV-lag (lag: '{layerName}').", PipePlanStatusKind.Warning);
                transaction.Commit();
                return;
            }

            if (!PipePlanRadiusStore.IsAcceptedCombo(system, type))
            {
                ReportMessage(document, $"{system} {type} understøttes ikke.", PipePlanStatusKind.Warning);
                transaction.Commit();
                return;
            }

            if (source.Closed)
            {
                ReportMessage(document, "Lukkede polylinjer understøttes ikke.", PipePlanStatusKind.Warning);
                transaction.Commit();
                return;
            }

            if (!PipePlanRadiusStore.TryGet(document.Database, system, type, dn, out double sharpCornerRadius) || sharpCornerRadius <= 0.0)
            {
                ReportMessage(document, $"Ingen bukkeradius for {system} {type} DN{dn}. Sæt den i PPSETTINGS.", PipePlanStatusKind.Warning);
                transaction.Commit();
                return;
            }

            if (!PipePlanReverseSolver.TryConvert(source, sharpCornerRadius, out PipePlanReverseSolverResult? reverseResult, out string reverseError) || reverseResult is null)
            {
                ReportMessage(document, reverseError, PipePlanStatusKind.Warning);
                transaction.Commit();
                return;
            }

            if (reverseResult.SharpCornerPositions.Count > 0)
            {
                double defaultSharpCornerRadius = sharpCornerRadius;
                double pipeWidth = PipePlanWidthCalculator.ResolveDrawingWidth(layerName);

                ReportEditorMessage(editor, $"Skarpe hjørner: {reverseResult.SharpCornerPositions.Count} stk. bukkes ved min radius {defaultSharpCornerRadius:0.##}.");
                markers.Show(document, reverseResult.SharpCornerPositions);

                using PipePlanPreviewManager preview = new(document);
                PipePlanSolver previewSolver = new();

                while (true)
                {
                    if (!PipePlanReverseSolver.TryConvert(source, sharpCornerRadius, out reverseResult, out reverseError) || reverseResult is null)
                    {
                        ReportMessage(document, reverseError, PipePlanStatusKind.Warning);
                        transaction.Commit();
                        return;
                    }

                    PipePlanAnalysis previewAnalysis = previewSolver.Analyze(reverseResult.ControlPoints, reverseResult.BendRadii);
                    preview.Show(previewAnalysis, pipeWidth);
                    editor.UpdateScreen();

                    bool radiusChanged = Math.Abs(sharpCornerRadius - defaultSharpCornerRadius) > 1e-9;
                    string promptMessage = radiusChanged
                        ? $"\nEnter to convert at radius {sharpCornerRadius:0.##}, input a different radius, [Default] to restore {defaultSharpCornerRadius:0.##}, or Esc to cancel: "
                        : $"\nEnter to convert at radius {sharpCornerRadius:0.##}, input a different radius, or Esc to cancel: ";

                    PromptDoubleOptions prompt = new(promptMessage)
                    {
                        AllowNegative = false,
                        AllowZero = false,
                        AllowNone = true,
                    };
                    if (radiusChanged)
                    {
                        prompt.Keywords.Add("Default");
                    }

                    PromptDoubleResult res = editor.GetDouble(prompt);

                    if (res.Status == PromptStatus.None)
                    {
                        break;
                    }
                    if (res.Status == PromptStatus.OK)
                    {
                        sharpCornerRadius = res.Value;
                        continue;
                    }
                    if (res.Status == PromptStatus.Keyword && string.Equals(res.StringResult, "Default", StringComparison.OrdinalIgnoreCase))
                    {
                        sharpCornerRadius = defaultSharpCornerRadius;
                        continue;
                    }

                    ReportMessage(document, "PPCONVERT annulleret.", PipePlanStatusKind.Info);
                    transaction.Commit();
                    return;
                }
            }

            PipePlanSolver solver = new();
            PipePlanAnalysis analysis = solver.Analyze(reverseResult.ControlPoints, reverseResult.BendRadii);
            if (!analysis.IsFeasible)
            {
                ReportMessage(document, $"Rekonstruktion fejlede: {analysis.Message}", PipePlanStatusKind.Warning);
                transaction.Commit();
                return;
            }

            Polyline sourceWrite = (Polyline)transaction.GetObject(pick.ObjectId, OpenMode.ForWrite);

            PipePlanStoredData metadata = new(
                system,
                type,
                dn,
                reverseResult.BendRadii,
                PipePlanRuntime.StateFor(document).StraightSnapToleranceText,
                reverseResult.ControlPoints);
            // In-place mutation: the converted polyline keeps its Handle/ObjectId
            // and any pre-existing third-party data. Color/Linetype/Normal/Elevation
            // are inherent to the un-erased entity, so no property copy is needed.
            PipePlanPolylineMutator.ApplyAnalysis(sourceWrite, analysis, metadata, layerName, transaction);

            transaction.Commit();

            int sharpCount = reverseResult.SharpCornerPositions.Count;
            string successMessage = sharpCount > 0
                ? $"Konverteret på lag {layerName}: {reverseResult.ControlPoints.Count} hjørner, {sharpCount} skarpe hjørne(r) bukket ved radius {sharpCornerRadius:0.##}."
                : $"Konverteret på lag {layerName}: {reverseResult.ControlPoints.Count} hjørner.";
            ReportMessage(document, successMessage, PipePlanStatusKind.Ok);
        }
        catch
        {
            transaction.Abort();
            throw;
        }
    }

    private static void RunDrawLoop(Document document)
    {
        Editor editor = document.Editor;

        while (true)
        {
            PromptPointResult result = PromptForNextDrawPoint(document);
            if (result.Status == PromptStatus.Keyword)
            {
                if (string.Equals(result.StringResult, "Tangent", StringComparison.OrdinalIgnoreCase))
                {
                    HandleTangentKeyword(document);
                }
                else
                {
                    HandleRadiusKeyword(document, result.StringResult);
                }
                continue;
            }

            if (result.Status == PromptStatus.None)
            {
                CompleteDraft(document);
                return;
            }

            if (result.Status != PromptStatus.OK)
            {
                PipePlanRuntime.StateFor(document).ResetDraft(clearStatus: false);
                PipePlanRuntime.StateFor(document).SetStatus("Tegning annulleret.", PipePlanStatusKind.Info);
                return;
            }

            bool allowStraightSnap = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            // Sticky tangent cache may still hold a snap whose source polyline has
            // since been erased, lost its PipePlan metadata, or had its endpoint
            // moved. Revalidate before commit; on failure the tangent is cleared
            // and ResolveCommittedCandidate falls back to a plain point.
            if (!PipePlanRuntime.StateFor(document).TryRevalidateLatestTangent(document, out string tangentFailure))
            {
                PipePlanRuntime.StateFor(document).SetStatus(
                    tangentFailure + " Tangent ignoreret.",
                    PipePlanStatusKind.Warning);
            }
            PipePlanCandidateResult candidate = PipePlanRuntime.StateFor(document).ResolveCommittedCandidate(result.Value, allowStraightSnap);
            if (!TryAcceptDrawCandidate(document, editor, candidate))
            {
                continue;
            }

            PipePlanRuntime.StateFor(document).AddCommittedCandidate(candidate);
            PipePlanRuntime.StateFor(document).ShowPreview(candidate.Analysis);
        }
    }

    private static PromptPointResult PromptForNextDrawPoint(Document document)
    {
        using CandidatePointTracker tracker = new(document, PipePlanRuntime.StateFor(document));

        string tangentSuffix = PipePlanRuntime.StateFor(document).IsTangentMode ? "Tangent (on)" : "Tangent (off)";
        PromptPointOptions options = new($"\nNext point [Radius/Default/{tangentSuffix}] or press Enter to finish: ")
        {
            BasePoint = PipePlanRuntime.StateFor(document).DraftPoints[^1],
            UseBasePoint = true,
            AllowNone = true
        };
        options.Keywords.Add("Radius");
        options.Keywords.Add("Default");
        options.Keywords.Add("Tangent");

        return document.Editor.GetPoint(options);
    }

    private static void HandleTangentKeyword(Document document)
    {
        bool newState = !PipePlanRuntime.StateFor(document).IsTangentMode;
        PipePlanRuntime.StateFor(document).SetTangentMode(newState);
        string message = newState
            ? "Tangent-mode ON. Hover over en PipePlan-polylinje."
            : "Tangent-mode OFF.";
        PipePlanRuntime.StateFor(document).SetStatus(message, PipePlanStatusKind.Info);
    }

    private static void HandleRadiusKeyword(Document document, string keyword)
    {
        if (string.Equals(keyword, "Radius", StringComparison.OrdinalIgnoreCase))
        {
            PromptForManualRadiusIterative(document);
            return;
        }

        if (string.Equals(keyword, "Default", StringComparison.OrdinalIgnoreCase))
        {
            PipePlanRuntime.StateFor(document).ClearManualRadius();
            string note = PipePlanRuntime.StateFor(document).ActiveContext is { Radius: var r } && r > 0.0
                ? $"Standard-radius gendannet ({r})."
                : "Standard-radius gendannet.";
            PipePlanRuntime.StateFor(document).SetStatus(note, PipePlanStatusKind.Info);
        }
    }

    private static void PromptForManualRadiusIterative(Document document)
    {
        Editor editor = document.Editor;
        bool anyValueEntered = false;

        while (true)
        {
            PromptDoubleOptions options = new("\nManual radius (Enter to confirm): ")
            {
                AllowNegative = false,
                AllowNone = true,
                AllowZero = false
            };

            PromptDoubleResult result = editor.GetDouble(options);
            if (result.Status == PromptStatus.None)
            {
                if (anyValueEntered)
                {
                    double current = PipePlanRuntime.StateFor(document).EffectiveRadius;
                    PipePlanRuntime.StateFor(document).SetStatus($"Manuel radius bekræftet ({current}).", PipePlanStatusKind.Ok);
                }
                else
                {
                    PipePlanRuntime.StateFor(document).SetStatus("Manuel radius uændret.", PipePlanStatusKind.Info);
                }
                return;
            }

            if (result.Status != PromptStatus.OK)
            {
                PipePlanRuntime.StateFor(document).SetStatus("Manuel radius annulleret.", PipePlanStatusKind.Info);
                return;
            }

            anyValueEntered = true;
            PipePlanRuntime.StateFor(document).SetManualRadius(result.Value);
            PipePlanRuntime.StateFor(document).SetStatus($"Forhåndsviser manuel radius {result.Value}.", PipePlanStatusKind.Info);
        }
    }

    private static void CompleteDraft(Document document)
    {
        if (PipePlanRuntime.StateFor(document).DraftPoints.Count >= 2)
        {
            PipePlanRuntime.StateFor(document).BakeDraft();
            return;
        }

        PipePlanRuntime.StateFor(document).ResetDraft(clearStatus: false);
        PipePlanRuntime.StateFor(document).SetStatus("Færre end to punkter.", PipePlanStatusKind.Warning);
    }

    private static bool TryAcceptDrawCandidate(Document document, Editor editor, PipePlanCandidateResult candidate)
    {
        PipePlanAnalysis analysis = candidate.Analysis;
        if (!analysis.IsFeasible)
        {
            ReportEditorMessage(editor, $"Punkt afvist: {analysis.Message}");
            PipePlanRuntime.StateFor(document).ShowPreview(analysis);
            PipePlanRuntime.StateFor(document).SetStatus(analysis.Message, PipePlanStatusKind.Error);
            return false;
        }

        return true;
    }

    private static bool TryInitializeDraw(Document document, out string errorMessage)
    {
        errorMessage = string.Empty;
        Editor editor = document.Editor;

        PromptKeywordOptions modeOptions = new("\nStart [New/Continue] <New>: ", "New Continue")
        {
            AllowNone = true
        };

        PromptResult modeResult = editor.GetKeywords(modeOptions);
        if (modeResult.Status == PromptStatus.Cancel)
        {
            PipePlanRuntime.StateFor(document).ClearPreview();
            PipePlanRuntime.StateFor(document).SetStatus("Tegning annulleret.", PipePlanStatusKind.Info);
            return false;
        }

        bool continueExisting = string.Equals(modeResult.StringResult, "Continue", StringComparison.OrdinalIgnoreCase);
        if (continueExisting)
        {
            return TryContinueExisting(document, out errorMessage);
        }

        if (!PipePlanRuntime.StateFor(document).InitializeForCurrentLayer(document.Database, out string layerError))
        {
            errorMessage = layerError;
            return false;
        }

        while (true)
        {
            string tangentSuffix = PipePlanRuntime.StateFor(document).IsTangentMode ? "Tangent (on)" : "Tangent (off)";
            PromptPointOptions firstPointOptions = new($"\nFirst point [Radius/Default/{tangentSuffix}]: ");
            firstPointOptions.Keywords.Add("Radius");
            firstPointOptions.Keywords.Add("Default");
            firstPointOptions.Keywords.Add("Tangent");

            PromptPointResult firstPointResult = editor.GetPoint(firstPointOptions);
            if (firstPointResult.Status == PromptStatus.Keyword)
            {
                if (string.Equals(firstPointResult.StringResult, "Tangent", StringComparison.OrdinalIgnoreCase))
                {
                    HandleTangentKeyword(document);
                }
                else
                {
                    HandleRadiusKeyword(document, firstPointResult.StringResult);
                }
                continue;
            }

            if (firstPointResult.Status != PromptStatus.OK)
            {
                PipePlanRuntime.StateFor(document).ClearPreview();
                PipePlanRuntime.StateFor(document).SetStatus("Tegning annulleret.", PipePlanStatusKind.Info);
                return false;
            }

            PipePlanRuntime.StateFor(document).AddDraftPoint(firstPointResult.Value);
            PipePlanRuntime.StateFor(document).RefreshDraftPreview();
            return true;
        }
    }

    private static bool TryContinueExisting(Document document, out string errorMessage)
    {
        errorMessage = string.Empty;
        Editor editor = document.Editor;

        PromptEntityOptions options = new("\nSelect a PipePlan object to continue from: ");
        options.SetRejectMessage("\nOnly PipePlan polylines are supported.");
        options.AddAllowedClass(typeof(Polyline), exactMatch: false);

        PromptEntityResult result = editor.GetEntity(options);
        if (result.Status != PromptStatus.OK)
        {
            PipePlanRuntime.StateFor(document).ClearPreview();
            PipePlanRuntime.StateFor(document).SetStatus("Tegning annulleret.", PipePlanStatusKind.Info);
            return false;
        }

        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(result.ObjectId, OpenMode.ForRead);
            if (!PipePlanMetadata.TryRead(polyline, transaction, out PipePlanStoredData? data) || data is null)
            {
                errorMessage = "Ikke et PipePlan-objekt. Kør PPCONVERT først.";
                return false;
            }

            if (!PipePlanGeometryValidator.TryValidateAgainstMetadata(polyline, data, out errorMessage))
            {
                return false;
            }

            if (!TryResolveEndpoint(result.PickedPoint, data.ControlPoints, out bool reverse))
            {
                errorMessage = "For få hjørner til at fortsætte fra.";
                return false;
            }

            transaction.Commit();
            PipePlanRuntime.StateFor(document).BeginDraftFromExisting(result.ObjectId, data, reverse);
            PipePlanRuntime.StateFor(document).SetStatus(
                $"Fortsætter {data.SizeDisplay} fra valgt endepunkt. Vælg næste punkt.",
                PipePlanStatusKind.Info);
            return true;
        }
        catch
        {
            transaction.Abort();
            throw;
        }
    }

    private static bool TryResolveEndpoint(Point3d pickedPoint, IReadOnlyList<Point3d> controlPoints, out bool reverse)
    {
        reverse = false;
        if (controlPoints.Count < 2)
        {
            return false;
        }

        double startDistance = PipePlanGeometryUtil.Distance2D(pickedPoint, controlPoints[0]);
        double endDistance = PipePlanGeometryUtil.Distance2D(pickedPoint, controlPoints[^1]);
        reverse = startDistance <= endDistance;
        return true;
    }

    private static void ReportMessage(Document document, string message, PipePlanStatusKind kind)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ReportEditorMessage(document.Editor, message);
        PipePlanRuntime.StateFor(document).SetStatus(message, kind);
    }

    private static void ReportEditorMessage(Editor editor, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        editor.WriteMessage($"\n{message}");
    }

    private static void HandleCommandException(Document document, string commandName, System.Exception exception)
    {
        prdDbg(exception);
        string message = $"{commandName} failed: {exception.Message}";
        ReportEditorMessage(document.Editor, message);
        PipePlanRuntime.StateFor(document).SetStatus(message, PipePlanStatusKind.Error);
    }
}
