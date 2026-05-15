using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using IntersectUtilities.MPE.PipePlan;
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
            ExecuteSettings();
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
        if (!PipePlanEditSession.TryCreate(document, PipePlanRuntime.State, out session, out string errorMessage) || session is null)
        {
            ReportMessage(document, errorMessage, PipePlanStatusKind.Warning);
            return false;
        }

        return true;
    }

    private static void RunEditLoop(Document document, PipePlanEditSession session)
    {
        Editor editor = document.Editor;
        PipePlanRuntime.State.SetStatus(
            $"Editing {session.SizeLabel} (R={session.RadiusDisplay}). Pick a control circle or segment square, or press Enter to finish.",
            PipePlanStatusKind.Info);

        while (true)
        {
            session.ShowHandles();

            PromptPointResult pickResult = PromptForEditHandle(editor);
            if (pickResult.Status == PromptStatus.None)
            {
                session.ClearVisuals();
                PipePlanRuntime.State.SetStatus("PPEdit finished.", PipePlanStatusKind.Info);
                return;
            }

            if (pickResult.Status != PromptStatus.OK)
            {
                session.ClearVisuals();
                PipePlanRuntime.State.SetStatus("PPEdit cancelled.", PipePlanStatusKind.Info);
                return;
            }

            if (!TryResolveEditHandle(session, editor, pickResult.Value, out PipePlanEditHandle? handle) || handle is null)
            {
                continue;
            }

            session.ClearVisuals();
            PromptPointResult dragResult = PromptForEditMove(document, session, handle);
            if (dragResult.Status == PromptStatus.Keyword &&
                string.Equals(dragResult.StringResult, "Radius", StringComparison.OrdinalIgnoreCase))
            {
                HandleVertexRadiusEdit(document, session, handle);
                continue;
            }

            if (dragResult.Status == PromptStatus.None)
            {
                PipePlanRuntime.State.ClearPreview();
                PipePlanRuntime.State.SetStatus("Edit cancelled. Pick another handle.", PipePlanStatusKind.Info);
                continue;
            }

            if (dragResult.Status != PromptStatus.OK)
            {
                session.ClearVisuals();
                PipePlanRuntime.State.SetStatus("PPEdit cancelled.", PipePlanStatusKind.Info);
                return;
            }

            ApplyEditCandidate(document, session, handle, dragResult.Value);
        }
    }

    private static void HandleVertexRadiusEdit(Document document, PipePlanEditSession session, PipePlanEditHandle handle)
    {
        if (handle.Kind != PipePlanEditHandleKind.Vertex)
        {
            return;
        }

        double current = handle.Index < session.CurrentBendRadii.Count
            ? session.CurrentBendRadii[handle.Index]
            : 0.0;

        Editor editor = document.Editor;
        double? pendingRadius = null;

        while (true)
        {
            string prompt = pendingRadius.HasValue
                ? $"\nPreviewing radius {pendingRadius.Value}. Enter to confirm, another value to preview, or [Default]: "
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
                if (TryPreviewDefaultVertexRadius(document, session, handle, out double defaultRadius))
                {
                    pendingRadius = defaultRadius;
                }
                continue;
            }

            if (res.Status == PromptStatus.None)
            {
                CommitPendingVertexRadius(document, session, handle, current, pendingRadius);
                return;
            }

            if (res.Status != PromptStatus.OK)
            {
                PipePlanRuntime.State.ClearPreview();
                PipePlanRuntime.State.SetStatus("Radius edit cancelled.", PipePlanStatusKind.Info);
                return;
            }

            if (!session.TryAnalyzeVertexRadius(handle.Index, res.Value, out PipePlanAnalysis previewAnalysis, out string previewError))
            {
                ReportEditorMessage(editor, $"Radius rejected: {previewError}");
                PipePlanRuntime.State.SetStatus(previewError, PipePlanStatusKind.Error);
                continue;
            }

            PipePlanRuntime.State.ShowPreview(previewAnalysis);
            pendingRadius = res.Value;
            PipePlanRuntime.State.SetStatus($"Previewing radius {res.Value}.", PipePlanStatusKind.Info);
        }
    }

    private static bool TryPreviewDefaultVertexRadius(
        Document document,
        PipePlanEditSession session,
        PipePlanEditHandle handle,
        out double defaultRadius)
    {
        defaultRadius = 0.0;
        PipePlanActiveContext? ctx = PipePlanRuntime.State.ActiveContext;
        if (ctx is null)
        {
            PipePlanRuntime.State.SetStatus("No active pipe context for default radius.", PipePlanStatusKind.Warning);
            return false;
        }

        if (!PipePlanRadiusStore.TryGet(document.Database, ctx.System, ctx.Type, ctx.Dn, out double resolved) || resolved <= 0.0)
        {
            PipePlanRuntime.State.SetStatus($"No default radius available for {ctx.System} {ctx.Type} DN{ctx.Dn}.", PipePlanStatusKind.Warning);
            return false;
        }

        if (!session.TryAnalyzeVertexRadius(handle.Index, resolved, out PipePlanAnalysis analysis, out string error))
        {
            ReportEditorMessage(document.Editor, $"Default radius rejected: {error}");
            PipePlanRuntime.State.SetStatus(error, PipePlanStatusKind.Error);
            return false;
        }

        PipePlanRuntime.State.ShowPreview(analysis);
        PipePlanRuntime.State.SetStatus($"Previewing default radius {resolved}.", PipePlanStatusKind.Info);
        defaultRadius = resolved;
        return true;
    }

    private static void CommitPendingVertexRadius(
        Document document,
        PipePlanEditSession session,
        PipePlanEditHandle handle,
        double current,
        double? pendingRadius)
    {
        PipePlanRuntime.State.ClearPreview();

        if (!pendingRadius.HasValue)
        {
            PipePlanRuntime.State.SetStatus("Radius unchanged.", PipePlanStatusKind.Info);
            return;
        }

        double committedRadius = pendingRadius.Value;
        if (Math.Abs(committedRadius - current) < 1e-9)
        {
            PipePlanRuntime.State.SetStatus("Radius unchanged.", PipePlanStatusKind.Info);
            return;
        }

        if (!session.TrySetVertexRadius(handle.Index, committedRadius, out string err))
        {
            ReportEditorMessage(document.Editor, $"Radius rejected: {err}");
            PipePlanRuntime.State.SetStatus(err, PipePlanStatusKind.Error);
            return;
        }

        PipePlanRuntime.State.SetStatus($"Bend radius at vertex updated to {committedRadius}.", PipePlanStatusKind.Ok);
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
        PipePlanEditSession session,
        Editor editor,
        Point3d pickedPoint,
        out PipePlanEditHandle? handle)
    {
        handle = null;
        if (!session.TryResolveHandle(pickedPoint, out handle, out string handleMessage) || handle is null)
        {
            ReportEditorMessage(editor, handleMessage);
            PipePlanRuntime.State.SetStatus(handleMessage, PipePlanStatusKind.Warning);
            return false;
        }

        return true;
    }

    private static PromptPointResult PromptForEditMove(
        Document document,
        PipePlanEditSession session,
        PipePlanEditHandle handle)
    {
        using PipePlanEditTracker tracker = new(document, PipePlanRuntime.State, session, handle);
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
            PipePlanRuntime.State.ClearPreview();
            ReportEditorMessage(document.Editor, $"Edit rejected: {candidate.Analysis.Message}");
            PipePlanRuntime.State.SetStatus(candidate.Analysis.Message, PipePlanStatusKind.Error);
            return;
        }

        session.Commit(candidate);
        PipePlanRuntime.State.ClearPreview();
        PipePlanRuntime.State.SetStatus("Edit applied. Pick another handle or press Enter to finish.", PipePlanStatusKind.Ok);
    }

    private static void ExecuteSettings()
    {
        PipePlanRuntime.State.EnsurePalette();
        PipePlanRuntime.State.SetStatus("Edit per-DN radii and click Save.", PipePlanStatusKind.Info);
    }

    private static void ExecuteDraw(Document document)
    {
        if (!NSPaletteAdapter.IsLoaded)
        {
            ReportMessage(document, "Load NSPalette first.", PipePlanStatusKind.Warning);
            return;
        }

        PipePlanRuntime.State.ResetDraft(clearStatus: false);

        if (!TryInitializeDraw(document, out string initializationError))
        {
            ReportMessage(document, initializationError, PipePlanStatusKind.Warning);
            return;
        }

        RunDrawLoop(document);
    }

    private static void RunDrawLoop(Document document)
    {
        Editor editor = document.Editor;

        while (true)
        {
            PromptPointResult result = PromptForNextDrawPoint(document);
            if (result.Status == PromptStatus.Keyword)
            {
                HandleRadiusKeyword(document, result.StringResult);
                continue;
            }

            if (result.Status == PromptStatus.None)
            {
                CompleteDraft();
                return;
            }

            if (result.Status != PromptStatus.OK)
            {
                PipePlanRuntime.State.RefreshDraftPreview();
                PipePlanRuntime.State.SetStatus("Drawing cancelled.", PipePlanStatusKind.Info);
                return;
            }

            bool allowStraightSnap = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            PipePlanCandidateResult candidate = PipePlanRuntime.State.ResolveCommittedCandidate(result.Value, allowStraightSnap);
            if (!TryAcceptDrawCandidate(editor, candidate))
            {
                continue;
            }

            PipePlanRuntime.State.AddCommittedCandidate(candidate);
            PipePlanRuntime.State.SetLatestAnalysis(candidate.Analysis);
            PipePlanRuntime.State.ShowPreview(candidate.Analysis);
        }
    }

    private static PromptPointResult PromptForNextDrawPoint(Document document)
    {
        using CandidatePointTracker tracker = new(document, PipePlanRuntime.State);

        PromptPointOptions options = new("\nNext point [Radius/Default] or press Enter to finish: ")
        {
            BasePoint = PipePlanRuntime.State.DraftPoints[^1],
            UseBasePoint = true,
            AllowNone = true
        };
        options.Keywords.Add("Radius");
        options.Keywords.Add("Default");

        return document.Editor.GetPoint(options);
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
            PipePlanRuntime.State.ClearManualRadius();
            string note = PipePlanRuntime.State.ActiveContext is { Radius: var r } && r > 0.0
                ? $"Default radius restored ({r})."
                : "Default radius restored.";
            PipePlanRuntime.State.SetStatus(note, PipePlanStatusKind.Info);
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
                    double current = PipePlanRuntime.State.EffectiveRadius;
                    PipePlanRuntime.State.SetStatus($"Manual radius confirmed at {current}.", PipePlanStatusKind.Ok);
                }
                else
                {
                    PipePlanRuntime.State.SetStatus("Manual radius unchanged.", PipePlanStatusKind.Info);
                }
                return;
            }

            if (result.Status != PromptStatus.OK)
            {
                PipePlanRuntime.State.SetStatus("Manual radius cancelled.", PipePlanStatusKind.Info);
                return;
            }

            anyValueEntered = true;
            PipePlanRuntime.State.SetManualRadius(result.Value);
            PipePlanRuntime.State.SetStatus($"Previewing manual radius {result.Value}.", PipePlanStatusKind.Info);
        }
    }

    private static void CompleteDraft()
    {
        if (PipePlanRuntime.State.DraftPoints.Count >= 2)
        {
            PipePlanRuntime.State.BakeDraft();
            return;
        }

        PipePlanRuntime.State.RefreshDraftPreview();
        PipePlanRuntime.State.SetStatus("Draft has fewer than two points.", PipePlanStatusKind.Warning);
    }

    private static bool TryAcceptDrawCandidate(Editor editor, PipePlanCandidateResult candidate)
    {
        PipePlanAnalysis analysis = candidate.Analysis;
        if (!analysis.IsFeasible)
        {
            ReportEditorMessage(editor, $"Point rejected: {analysis.Message}");
            PipePlanRuntime.State.ShowPreview(analysis);
            PipePlanRuntime.State.SetStatus(analysis.Message, PipePlanStatusKind.Error);
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
            PipePlanRuntime.State.SetStatus("Drawing cancelled.", PipePlanStatusKind.Info);
            return false;
        }

        bool continueExisting = string.Equals(modeResult.StringResult, "Continue", StringComparison.OrdinalIgnoreCase);
        if (continueExisting)
        {
            return TryContinueExisting(document, out errorMessage);
        }

        if (!PipePlanRuntime.State.InitializeForCurrentLayer(document.Database, out string layerError))
        {
            errorMessage = layerError;
            return false;
        }

        while (true)
        {
            PromptPointOptions firstPointOptions = new("\nFirst point [Radius/Default]: ");
            firstPointOptions.Keywords.Add("Radius");
            firstPointOptions.Keywords.Add("Default");

            PromptPointResult firstPointResult = editor.GetPoint(firstPointOptions);
            if (firstPointResult.Status == PromptStatus.Keyword)
            {
                HandleRadiusKeyword(document, firstPointResult.StringResult);
                continue;
            }

            if (firstPointResult.Status != PromptStatus.OK)
            {
                PipePlanRuntime.State.SetStatus("Drawing cancelled.", PipePlanStatusKind.Info);
                return false;
            }

            PipePlanRuntime.State.AddDraftPoint(firstPointResult.Value);
            PipePlanRuntime.State.RefreshDraftPreview();
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
            PipePlanRuntime.State.SetStatus("Drawing cancelled.", PipePlanStatusKind.Info);
            return false;
        }

        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(result.ObjectId, OpenMode.ForRead);
            if (!PipePlanMetadata.TryRead(polyline, transaction, out PipePlanStoredData? data) || data is null)
            {
                errorMessage = "The selected polyline is not a metadata-enabled PipePlan object.";
                return false;
            }

            if (!PipePlanGeometryValidator.TryValidateAgainstMetadata(polyline, data, out errorMessage))
            {
                return false;
            }

            if (!TryResolveEndpoint(result.PickedPoint, data.ControlPoints, out bool reverse))
            {
                errorMessage = "The selected PipePlan object does not have enough control points to continue.";
                return false;
            }

            transaction.Commit();
            PipePlanRuntime.State.BeginDraftFromExisting(result.ObjectId, data, reverse);
            PipePlanRuntime.State.SetStatus(
                $"Continuing {data.SizeDisplay} from the selected endpoint. Pick the next point.",
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
        PipePlanRuntime.State.SetStatus(message, kind);
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
        PipePlanRuntime.State.SetStatus(message, PipePlanStatusKind.Error);
    }
}
