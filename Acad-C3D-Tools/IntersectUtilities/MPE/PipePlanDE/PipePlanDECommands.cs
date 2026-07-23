using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.MPE.PipePlan;
using IntersectUtilities.MPE.PipePlanDE;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>PDSETTINGS</command>
    /// <summary>Shows the PipePlanDE settings palette: edit the per-DN parameter table (z1, d, x, … b, B, B1) next to the Regel-Grabenprofil reference diagram. Overrides are saved to the active drawing. Pick the DN to draw in the PDDRAW window instead.</summary>
    /// <category>PipePlanDE</category>
    [CommandMethod("PDSETTINGS")]
    public void PipePlanDESettings()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            PipePlanDEState state = PipePlanDERuntime.StateFor(document);
            PipePlanDERuntime.SettingsPalette.RebindTo(state);
            PipePlanDERuntime.SettingsPalette.Show();
        }
        catch (System.Exception exception)
        {
            HandleDECommandException(document, "PDSETTINGS", exception);
        }
    }

    /// <command>PDDRAW</command>
    /// <summary>Opens the dimension picker and draws a single-pipe run. The window stays open to select the drawing size; if no DN is selected yet, PDDRAW just shows it and waits. With a DN selected, you draw the routing centreline interactively, and on Enter the centreline (layer 0-Centerline) plus two mantle-OD-wide frem/retur polylines are placed, corners filleted to the DN's minimum elastic bending radius (inner pipe = R_min, centreline and outer derived from the offsets). A corner too tight for that radius turns the preview red and is rejected on Enter. The [Straight] keyword toggles filleting off entirely — sharp mitered corners with no arcs. Closing the window and running PDDRAW reopens it.</summary>
    /// <category>PipePlanDE</category>
    [CommandMethod("PDDRAW")]
    public void PipePlanDEDraw()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            // Always surface the size picker first — this is the "show the window"
            // half of PDDRAW. Drawing only proceeds once a DN has been chosen there.
            PipePlanDEState state = PipePlanDERuntime.StateFor(document);
            PipePlanDERuntime.SizePalette.RebindTo(state);
            PipePlanDERuntime.SizePalette.Show();

            ExecuteDrawDE(document);
        }
        catch (System.Exception exception)
        {
            HandleDECommandException(document, "PDDRAW", exception);
        }
    }

    /// <command>PDTRENCH</command>
    /// <summary>Select one or more PDDRAW centrelines to draw their trench: a SOLID, 80%-transparent hatch on the "Gravearbejde" layer, each axis sized to its stored DN's Regelgrabenbreite (B). Each axis is buffered by B/2 (clean at sharp bends and along the fillet arcs); where trenches meet they are merged into one hatch, and separate runs stay separate.</summary>
    /// <category>PipePlanDE</category>
    [CommandMethod("PDTRENCH")]
    public void PipePlanDETrench()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteTrenchDE(document);
        }
        catch (System.Exception exception)
        {
            HandleDECommandException(document, "PDTRENCH", exception);
        }
    }

    /// <command>PDEDIT</command>
    /// <summary>Edits a PDDRAW run (pick any of its three polylines): drag the orange vertex grips or yellow segment grips to reshape, [Add]/[Delete] corners, [Continue] to extend the run from an endpoint (like PPDRAW Continue), or [Radius] to set a single corner's bending radius (floored at the DN's minimum). The whole run (centreline + frem + retur) re-solves live and re-bakes on commit. Only runs drawn after filleting was added are editable; runs drawn without bends (Straight) must be redrawn.</summary>
    /// <category>PipePlanDE</category>
    [CommandMethod("PDEDIT")]
    public void PipePlanDEEdit()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteEditDE(document);
        }
        catch (System.Exception exception)
        {
            HandleDECommandException(document, "PDEDIT", exception);
        }
    }

    /// <command>PDANNOTATE</command>
    /// <summary>Select PDDRAW centrelines to dimension them: each corner-to-corner routing leg gets an aligned dimension (like DimAligned), split into separate dimensions where another selected centreline crosses it, plus a DIMARC arc-length dimension for each fillet bend. Type [Settings] (S) first to pick the dimension style and offset from a dropdown (persisted per drawing). Dimensions land on layer PD-Anno.</summary>
    /// <category>PipePlanDE</category>
    [CommandMethod("PDANNOTATE")]
    public void PipePlanDEAnnotate()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteAnnotateDE(document);
        }
        catch (System.Exception exception)
        {
            HandleDECommandException(document, "PDANNOTATE", exception);
        }
    }

    private static void ExecuteDrawDE(Document document)
    {
        Editor editor = document.Editor;
        PipePlanDEState state = PipePlanDERuntime.StateFor(document);

        if (state.ActiveDn is not int dn)
        {
            ReportDE(editor, "Vælg en dimension i Tegn-vinduet og kør PDDRAW igen.");
            PipePlanDERuntime.SizePalette.SetStatus("Vælg en dimension for at tegne.", PipePlanStatusKind.Warning);
            return;
        }

        PipePlanDEParameters? parameters = PipePlanDEParameterStore.GetEffective(document.Database, dn);
        if (parameters is null)
        {
            ReportDE(editor, $"Ingen parametre for DN {dn}.");
            return;
        }

        if (!TryCollectCenterline(document, dn, parameters, out List<Point3d> points, out List<double> radii, out bool flip, out bool straight))
        {
            return;
        }

        if (points.Count < 2)
        {
            ReportDE(editor, "Færre end to punkter — intet tegnet.");
            return;
        }

        BakeDE(document, dn, parameters, points, radii, flip, straight, state.ActiveDepth);
    }

    private static bool TryCollectCenterline(
        Document document, int dn, PipePlanDEParameters parameters,
        out List<Point3d> points, out List<double> radii, out bool flip, out bool straight)
    {
        Editor editor = document.Editor;
        // Assign the out lists up front so local functions/lambdas can capture the locals
        // (out parameters themselves may not be captured).
        List<Point3d> pts = [];
        List<double> rad = [];
        points = pts;
        radii = rad;
        flip = false;
        straight = false;
        bool flipState = false;
        bool straightState = false;

        if (!PipePlanDEBendRadiusTable.TryGet(dn, out double tableRmin))
        {
            ReportDE(editor, $"Ingen bukkeradius for DN {dn}.");
            return false;
        }

        double? manualRadius = null;
        double Effective() => manualRadius ?? tableRmin;

        PromptPointResult first = editor.GetPoint(new PromptPointOptions($"\nDN {dn}: Første punkt på centerlinje: "));
        if (first.Status != PromptStatus.OK)
        {
            ReportDE(editor, "PDDRAW annulleret.");
            return false;
        }

        pts.Add(first.Value);
        rad.Add(0.0);

        using PipePlanDEPreviewManager preview = new();

        // Iterative manual-radius prompt: enforce the DN's table R_min as a floor. Only
        // affects corners committed AFTER it is set (already-committed corners keep theirs).
        void PromptRadius()
        {
            while (true)
            {
                PromptDoubleOptions opts = new($"\nBukkeradius for følgende hjørner (min {tableRmin:0.###} m, Enter bekræfter): ")
                {
                    AllowNegative = false,
                    AllowZero = false,
                    AllowNone = true
                };

                PromptDoubleResult r = editor.GetDouble(opts);
                if (r.Status == PromptStatus.None)
                {
                    ReportDE(editor, manualRadius is double mm ? $"Radius {mm:0.###} m valgt." : "Radius uændret.");
                    return;
                }

                if (r.Status != PromptStatus.OK)
                {
                    ReportDE(editor, "Radius annulleret.");
                    return;
                }

                if (!PipePlanDEBendRadiusTable.Validate(dn, r.Value, out string verr))
                {
                    ReportDE(editor, verr);
                    continue;
                }

                manualRadius = r.Value;
                ReportDE(editor, $"Radius {r.Value:0.###} m for følgende hjørner.");
            }
        }

        while (true)
        {
            PromptPointResult next;
            using (new PipePlanDEPointTracker(document, preview, pts, rad, Effective, parameters, () => flipState, () => straightState))
            {
                string radLabel = manualRadius is double m ? $"R={m:0.###}" : $"R={tableRmin:0.###}(std)";
                PromptPointOptions options = new($"\nNæste punkt [{radLabel}] [Flip][Straight][Radius/Default] (Ctrl=snap), Enter afslutter: ")
                {
                    BasePoint = pts[^1],
                    UseBasePoint = true,
                    AllowNone = true
                };
                options.Keywords.Add("Flip");
                options.Keywords.Add("Straight");
                options.Keywords.Add("Radius");
                options.Keywords.Add("Default");

                next = editor.GetPoint(options);
            }

            if (next.Status == PromptStatus.Keyword)
            {
                if (string.Equals(next.StringResult, "Flip", StringComparison.OrdinalIgnoreCase))
                {
                    flipState = !flipState;
                    preview.Show(pts, rad, parameters, flipState, straightState);
                    editor.UpdateScreen();
                    ReportDE(editor, flipState ? "Frem/retur byttet." : "Frem/retur normal.");
                }
                else if (string.Equals(next.StringResult, "Straight", StringComparison.OrdinalIgnoreCase))
                {
                    straightState = !straightState;
                    preview.Show(pts, rad, parameters, flipState, straightState);
                    editor.UpdateScreen();
                    ReportDE(editor, straightState ? "Skarpe hjørner (uden buk)." : "Bukkeradius aktiv.");
                }
                else if (string.Equals(next.StringResult, "Radius", StringComparison.OrdinalIgnoreCase))
                {
                    PromptRadius();
                }
                else if (string.Equals(next.StringResult, "Default", StringComparison.OrdinalIgnoreCase))
                {
                    manualRadius = null;
                    ReportDE(editor, $"Standard-bukkeradius gendannet (DN {dn}: {tableRmin:0.###} m).");
                }

                continue;
            }

            if (next.Status == PromptStatus.None)
            {
                flip = flipState;
                straight = straightState;
                return true;
            }

            if (next.Status != PromptStatus.OK)
            {
                ReportDE(editor, "PDDRAW annulleret.");
                pts.Clear();
                rad.Clear();
                return false;
            }

            (Point3d committed, _) = PipePlanDESnap.Resolve(
                next.Value, pts, PipePlanDESnap.IsCtrlHeld(), PipePlanDESnap.GetSnapTolerance(editor));
            pts.Add(committed);
            rad.Add(0.0);
            // The point that just became interior (second-to-last) locks in the active radius.
            if (pts.Count >= 3)
            {
                rad[pts.Count - 2] = Effective();
            }

            preview.Show(pts, rad, parameters, flipState, straightState);
            editor.UpdateScreen();
        }
    }

    private static void BakeDE(Document document, int dn, PipePlanDEParameters parameters, IReadOnlyList<Point3d> points, IReadOnlyList<double> radii, bool flip, bool straight, PipePlanDETrenchDepth depth)
    {
        Editor editor = document.Editor;

        using DocumentLock documentLock = document.LockDocument();
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            if (!PipePlanDEPolylineWriter.TryWrite(document.Database, transaction, points, radii, dn, parameters, flip, straight, depth, tokenOverride: null, out _, out _, out string writeError))
            {
                transaction.Abort();
                ReportDE(editor, $"Kunne ikke tegne: {writeError}");
                return;
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Abort();
            throw;
        }

        ReportDE(editor, $"Tegnet DN {dn}: centerlinje + frem/retur ({points.Count} hjørner{(straight ? ", skarpe hjørner" : "")}).");
    }

    private static void ExecuteTrenchDE(Document document)
    {
        Editor editor = document.Editor;

        // Select one OR MANY axis polylines; where their trenches meet, the hatches merge.
        PromptSelectionOptions selectionOptions = new()
        {
            MessageForAdding = "\nVælg akse-polylinjer (0-Centerline) — flere kan vælges: "
        };
        SelectionFilter filter = new([new TypedValue((int)DxfCode.Start, "LWPOLYLINE")]);

        PromptSelectionResult selection = editor.GetSelection(selectionOptions, filter);
        if (selection.Status != PromptStatus.OK)
        {
            ReportDE(editor, "PDTRENCH annulleret.");
            return;
        }

        using DocumentLock documentLock = document.LockDocument();
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            List<PipePlanDETrenchRun> runs = new();
            int skipped = 0;
            foreach (ObjectId id in selection.Value.GetObjectIds())
            {
                if (transaction.GetObject(id, OpenMode.ForRead) is not Polyline polyline)
                {
                    skipped++;
                    continue;
                }

                if (!PipePlanDEMetadata.TryRead(polyline, transaction, out PipePlanDEStoredData? data)
                    || data is null || data.Role != PipePlanDERole.Centerline)
                {
                    skipped++;
                    continue;
                }

                PipePlanDEParameters? parameters = PipePlanDEParameterStore.GetEffective(document.Database, data.Dn);
                if (parameters is null)
                {
                    skipped++;
                    continue;
                }

                // Depth (baked at PDDRAW time) picks the Regelgrabenbreite: ≤ 1.3 m → B,
                // > 1.3 m → the wider B1. Each axis may use a different width.
                double trenchWidth = data.Depth == PipePlanDETrenchDepth.Deep ? parameters.B1 : parameters.B;
                List<Point3d> axisPoints = DensifyAxis(polyline);
                if (axisPoints.Count >= 2)
                {
                    runs.Add(new PipePlanDETrenchRun(axisPoints, trenchWidth));
                }
                else
                {
                    skipped++;
                }
            }

            if (runs.Count == 0)
            {
                transaction.Commit();
                ReportDE(editor, "Ingen gyldige akse-polylinjer valgt (vælg 0-Centerline, ikke rør).");
                return;
            }

            if (!PipePlanDETrenchWriter.TryWrite(document.Database, transaction, runs, out string writeError))
            {
                transaction.Abort();
                ReportDE(editor, $"Kunne ikke tegne grav: {writeError}");
                return;
            }

            transaction.Commit();
            // A freshly created hatch often doesn't display its fill until a regen.
            editor.Regen();
            string skippedNote = skipped > 0 ? $" ({skipped} sprunget over)" : string.Empty;
            ReportDE(editor, $"Grav tegnet for {runs.Count} akse(r){skippedNote} på laget {PipePlanDETrenchWriter.TrenchLayer}.");
        }
        catch
        {
            transaction.Abort();
            throw;
        }
    }

    // Samples the axis polyline into points, subdividing ARC segments so the trench buffer
    // follows the filleted centreline instead of cutting across the arc's chord. Straight
    // segments keep just their endpoints.
    private static List<Point3d> DensifyAxis(Polyline polyline)
    {
        const double maxArcChord = 2.0; // m — keeps arc chord error < ~5 cm at R = 13 m.

        List<Point3d> points = new();
        int vertexCount = polyline.NumberOfVertices;
        int segmentCount = polyline.Closed ? vertexCount : vertexCount - 1;

        for (int i = 0; i < segmentCount; i++)
        {
            points.Add(polyline.GetPoint3dAt(i));

            if (polyline.GetSegmentType(i) != SegmentType.Arc)
            {
                continue;
            }

            double startDist = polyline.GetDistanceAtParameter(i);
            double endDist = polyline.GetDistanceAtParameter(i + 1);
            double length = endDist - startDist;
            int steps = Math.Max(2, (int)Math.Ceiling(length / maxArcChord));
            for (int k = 1; k < steps; k++)
            {
                points.Add(polyline.GetPointAtDist(startDist + (length * k / steps)));
            }
        }

        if (!polyline.Closed)
        {
            points.Add(polyline.GetPoint3dAt(vertexCount - 1));
        }

        return points;
    }

    private static void ReportDE(Editor editor, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        editor.WriteMessage($"\n{message}");
    }

    private static void ExecuteEditDE(Document document)
    {
        Editor editor = document.Editor;

        PromptEntityOptions options = new("\nVælg et PDDRAW-rør at redigere: ");
        options.SetRejectMessage("\nKun polylinjer understøttes.");
        options.AddAllowedClass(typeof(Polyline), exactMatch: false);

        PromptEntityResult pick = editor.GetEntity(options);
        if (pick.Status != PromptStatus.OK)
        {
            ReportDE(editor, "PDEDIT annulleret.");
            return;
        }

        if (!PipePlanDEEditSession.TryCreateFrom(document, pick.ObjectId, out PipePlanDEEditSession? session, out string error) || session is null)
        {
            ReportDE(editor, error);
            return;
        }

        using (session)
        {
            RunEditLoopDE(document, session);
        }
    }

    private static void RunEditLoopDE(Document document, PipePlanDEEditSession session)
    {
        Editor editor = document.Editor;
        ReportDE(editor, $"Redigerer {session.SizeLabel}. Vælg håndtag, [Add/Delete/Continue], eller Enter for at afslutte.");

        while (true)
        {
            session.ShowHandles();

            PromptPointOptions options = new("\nVælg håndtag [Add/Delete/Continue] eller Enter for at afslutte: ") { AllowNone = true };
            options.Keywords.Add("Add");
            options.Keywords.Add("Delete");
            options.Keywords.Add("Continue");
            PromptPointResult pick = editor.GetPoint(options);

            if (pick.Status == PromptStatus.Keyword)
            {
                if (string.Equals(pick.StringResult, "Add", StringComparison.OrdinalIgnoreCase))
                {
                    RunAddVertexModeDE(document, session);
                }
                else if (string.Equals(pick.StringResult, "Delete", StringComparison.OrdinalIgnoreCase))
                {
                    RunDeleteVertexModeDE(document, session);
                }
                else if (string.Equals(pick.StringResult, "Continue", StringComparison.OrdinalIgnoreCase))
                {
                    RunContinueModeDE(document, session);
                }

                continue;
            }

            if (pick.Status == PromptStatus.None)
            {
                session.ClearVisuals();
                ReportDE(editor, "PDEDIT afsluttet.");
                return;
            }

            if (pick.Status != PromptStatus.OK)
            {
                session.ClearVisuals();
                ReportDE(editor, "PDEDIT annulleret.");
                return;
            }

            if (!session.TryResolveHandle(pick.Value, out PipePlanEditHandle? handle, out string message) || handle is null)
            {
                ReportDE(editor, message);
                continue;
            }

            RunHandleMoveLoopDE(document, session, handle);
        }
    }

    private static void RunHandleMoveLoopDE(Document document, PipePlanDEEditSession session, PipePlanEditHandle handle)
    {
        Editor editor = document.Editor;

        while (true)
        {
            session.ClearVisuals();

            Point3d? lastDrag;
            PromptPointResult drag;
            using (PipePlanDEEditMoveTracker tracker = new(document, session, handle))
            {
                string radiusSuffix = handle.Kind == PipePlanEditHandleKind.Vertex ? " [Radius]" : "";
                PromptPointOptions options = new($"\nTræk håndtaget{radiusSuffix}, klik for at placere, eller Enter for at fortryde: ")
                {
                    BasePoint = handle.GripPoint,
                    UseBasePoint = true,
                    AllowNone = true
                };
                if (handle.Kind == PipePlanEditHandleKind.Vertex)
                {
                    options.Keywords.Add("Radius");
                }

                drag = editor.GetPoint(options);
                lastDrag = tracker.LastPoint;
            }

            if (drag.Status == PromptStatus.Keyword &&
                string.Equals(drag.StringResult, "Radius", StringComparison.OrdinalIgnoreCase))
            {
                HandleVertexRadiusEditDE(document, session, handle);
                continue;
            }

            if (drag.Status == PromptStatus.OK)
            {
                ApplyEditCandidateDE(document, session, handle, drag.Value);
                session.ClearPendingRadius();
                return;
            }

            if (drag.Status == PromptStatus.None)
            {
                // Enter with a pending radius = commit the radius change (at the last previewed
                // position, or the grip if the cursor never moved). Otherwise discard.
                if (session.TryGetPendingRadius(out _, out _))
                {
                    ApplyEditCandidateDE(document, session, handle, lastDrag ?? handle.GripPoint);
                }
                else
                {
                    session.ClearPreview();
                    ReportDE(editor, "Fortrudt. Vælg et andet håndtag.");
                }

                session.ClearPendingRadius();
                return;
            }

            session.ClearPendingRadius();
            session.ClearVisuals();
            ReportDE(editor, "PDEDIT annulleret.");
            return;
        }
    }

    private static void ApplyEditCandidateDE(Document document, PipePlanDEEditSession session, PipePlanEditHandle handle, Point3d point)
    {
        Editor editor = document.Editor;
        PipePlanDEEditCandidate candidate = session.BuildCandidate(handle, point);
        if (!candidate.Feasible)
        {
            session.ClearPreview();
            ReportDE(editor, $"Kan ikke flytte: {candidate.Message}");
            return;
        }

        if (session.Commit(candidate, out string error))
        {
            session.ClearPreview();
            ReportDE(editor, "Håndtag flyttet.");
        }
        else
        {
            ReportDE(editor, $"Kunne ikke gemme: {error}");
        }
    }

    private static void HandleVertexRadiusEditDE(Document document, PipePlanDEEditSession session, PipePlanEditHandle handle)
    {
        if (handle.Kind != PipePlanEditHandleKind.Vertex)
        {
            return;
        }

        Editor editor = document.Editor;
        double current = handle.Index >= 0 && handle.Index < session.RMinRadii.Count ? session.RMinRadii[handle.Index] : 0.0;

        while (true)
        {
            PromptDoubleOptions options = new($"\nBukkeradius for hjørne (min DN {session.Dn}, nu {current:0.###} m, Enter bekræfter): ")
            {
                AllowNegative = false,
                AllowZero = false,
                AllowNone = true
            };

            PromptDoubleResult result = editor.GetDouble(options);
            if (result.Status == PromptStatus.None)
            {
                ReportDE(editor, session.TryGetPendingRadius(out _, out double v) ? $"Radius {v:0.###} m valgt (Enter for at anvende)." : "Radius uændret.");
                return;
            }

            if (result.Status != PromptStatus.OK)
            {
                ReportDE(editor, "Radius annulleret.");
                return;
            }

            if (!PipePlanDEBendRadiusTable.Validate(session.Dn, result.Value, out string verr))
            {
                ReportDE(editor, verr);
                continue;
            }

            session.SetPendingRadius(handle.Index, result.Value);
            session.ShowCandidatePreview(session.BuildCandidate(handle, handle.GripPoint));
            editor.UpdateScreen();
            current = result.Value;
        }
    }

    private static void RunAddVertexModeDE(Document document, PipePlanDEEditSession session)
    {
        Editor editor = document.Editor;
        if (!TryPromptInsertRadiusDE(document, session, out double radius))
        {
            return;
        }

        while (true)
        {
            session.ShowHandles();

            PromptPointResult pick;
            using (new PipePlanDEInsertTracker(document, session, radius))
            {
                PromptPointOptions options = new("\nPlacer nyt hjørne på et segment [Radius/Back] eller Enter for at afslutte: ") { AllowNone = true };
                options.Keywords.Add("Radius");
                options.Keywords.Add("Back");
                pick = editor.GetPoint(options);
            }

            if (pick.Status == PromptStatus.Keyword)
            {
                if (string.Equals(pick.StringResult, "Radius", StringComparison.OrdinalIgnoreCase))
                {
                    TryPromptInsertRadiusDE(document, session, out radius);
                    continue;
                }

                session.ClearPreview();
                return;
            }

            if (pick.Status != PromptStatus.OK)
            {
                session.ClearPreview();
                return;
            }

            PipePlanDEEditCandidate candidate = session.BuildNearestInsertCandidate(pick.Value, radius, out _);
            if (!candidate.Feasible)
            {
                ReportDE(editor, $"Kan ikke indsætte hjørne: {candidate.Message}");
                continue;
            }

            if (session.Commit(candidate, out string error))
            {
                session.ClearPreview();
                ReportDE(editor, $"Hjørne tilføjet (R={radius:0.###} m). Tilføj flere, eller Back.");
            }
            else
            {
                ReportDE(editor, $"Kunne ikke gemme: {error}");
            }
        }
    }

    private static bool TryPromptInsertRadiusDE(Document document, PipePlanDEEditSession session, out double radius)
    {
        Editor editor = document.Editor;
        radius = 0.0;
        if (!session.TryGetInsertRadius(out double defaultRadius, out string radiusError))
        {
            ReportDE(editor, radiusError);
            return false;
        }

        PromptDoubleOptions options = new($"\nBukkeradius for nyt hjørne <{defaultRadius:0.###}> [Default]: ")
        {
            AllowNegative = false,
            AllowZero = false,
            AllowNone = true
        };
        options.Keywords.Add("Default");

        PromptDoubleResult result = editor.GetDouble(options);
        if (result.Status == PromptStatus.None ||
            (result.Status == PromptStatus.Keyword && string.Equals(result.StringResult, "Default", StringComparison.OrdinalIgnoreCase)))
        {
            radius = defaultRadius;
            return true;
        }

        if (result.Status == PromptStatus.OK)
        {
            if (!PipePlanDEBendRadiusTable.Validate(session.Dn, result.Value, out string verr))
            {
                ReportDE(editor, $"{verr} Bruger standard {defaultRadius:0.###} m.");
                radius = defaultRadius;
                return true;
            }

            radius = result.Value;
            return true;
        }

        ReportDE(editor, "Indsætning annulleret.");
        return false;
    }

    private static void RunDeleteVertexModeDE(Document document, PipePlanDEEditSession session)
    {
        Editor editor = document.Editor;

        while (true)
        {
            session.ShowHandles();

            PromptPointResult pick;
            using (new PipePlanDEDeleteTracker(document, session))
            {
                PromptPointOptions options = new("\nVælg hjørne at slette [Back] eller Enter for at afslutte: ") { AllowNone = true };
                options.Keywords.Add("Back");
                pick = editor.GetPoint(options);
            }

            if (pick.Status != PromptStatus.OK)
            {
                session.ClearPreview();
                return;
            }

            if (!session.TryGetNearestVertexIndex(pick.Value, session.GetPickTolerance(), out int vertexIndex))
            {
                ReportDE(editor, "Intet hjørne valgt. Klik tættere på et hjørne.");
                continue;
            }

            if (!session.TryBuildRemoveVertexCandidate(vertexIndex, out PipePlanDEEditCandidate? candidate, out string error) || candidate is null)
            {
                ReportDE(editor, $"Kan ikke slette hjørne: {error}");
                continue;
            }

            if (session.Commit(candidate, out string commitError))
            {
                session.ClearPreview();
                ReportDE(editor, "Hjørne slettet. Slet flere, eller Back.");
            }
            else
            {
                ReportDE(editor, $"Kunne ikke gemme: {commitError}");
            }
        }
    }

    /// <summary>
    /// Continue (extend) an existing run from one of its endpoints — the PDEDIT analogue of
    /// PPDRAW's Continue. Mirrors PipePlanCommands.TryResolveEndpoint: the picked end nearest
    /// the START reverses the working list so new points always append at the back. New corners
    /// snapshot the active radius (Radius/Default keywords), and Enter re-bakes the whole run.
    /// </summary>
    private static void RunContinueModeDE(Document document, PipePlanDEEditSession session)
    {
        Editor editor = document.Editor;

        PromptPointResult endPick = editor.GetPoint(new PromptPointOptions("\nVælg endepunkt at fortsætte fra: "));
        if (endPick.Status != PromptStatus.OK)
        {
            ReportDE(editor, "Fortsæt annulleret.");
            return;
        }

        IReadOnlyList<Point3d> cps = session.ControlPoints;
        if (cps.Count < 2)
        {
            ReportDE(editor, "For få hjørner til at fortsætte fra.");
            return;
        }

        // reverse when the pick is nearer the start (PipePlanCommands.TryResolveEndpoint).
        bool reverse = cps[0].DistanceTo(endPick.Value) <= cps[^1].DistanceTo(endPick.Value);
        int originalCount = cps.Count;

        // Working lists in DRAWING orientation (active end at the back). Reversed back to the
        // stored orientation when building the candidate, so preview colours + flip stay correct.
        List<Point3d> drawCps = reverse ? Enumerable.Reverse(cps).ToList() : cps.ToList();
        List<double> drawRadii = reverse ? Enumerable.Reverse(session.RMinRadii).ToList() : session.RMinRadii.ToList();

        if (!PipePlanDEBendRadiusTable.TryGet(session.Dn, out double tableRmin))
        {
            ReportDE(editor, $"Ingen bukkeradius for DN {session.Dn}.");
            return;
        }

        double? manualRadius = null;
        double Effective() => manualRadius ?? tableRmin;

        // Rebuild the full run (stored orientation) from the working lists, optionally with a
        // trailing candidate; the newly-interior second-to-last corner snapshots Effective().
        List<Point3d> BuildFinal(Point3d? candidate, out List<double> finalRadii)
        {
            List<Point3d> full = new(drawCps);
            List<double> radii = new(drawRadii);
            if (candidate is Point3d c)
            {
                full.Add(c);
                radii.Add(0.0);
                if (full.Count >= 3)
                {
                    radii[full.Count - 2] = Effective();
                }
            }

            if (reverse)
            {
                full.Reverse();
                radii.Reverse();
            }

            finalRadii = radii;
            return full;
        }

        void PromptContinueRadius()
        {
            while (true)
            {
                PromptDoubleOptions opts = new($"\nBukkeradius for følgende hjørner (min {tableRmin:0.###} m, Enter bekræfter): ")
                {
                    AllowNegative = false,
                    AllowZero = false,
                    AllowNone = true
                };
                PromptDoubleResult r = editor.GetDouble(opts);
                if (r.Status == PromptStatus.None)
                {
                    ReportDE(editor, manualRadius is double mm ? $"Radius {mm:0.###} m valgt." : "Radius uændret.");
                    return;
                }

                if (r.Status != PromptStatus.OK)
                {
                    ReportDE(editor, "Radius annulleret.");
                    return;
                }

                if (!PipePlanDEBendRadiusTable.Validate(session.Dn, r.Value, out string verr))
                {
                    ReportDE(editor, verr);
                    continue;
                }

                manualRadius = r.Value;
                ReportDE(editor, $"Radius {r.Value:0.###} m for følgende hjørner.");
            }
        }

        session.ClearVisuals();

        while (true)
        {
            PromptPointResult next;
            using (new PipePlanDEContinueTracker(document, session, cursor =>
            {
                (Point3d snapped, _) = PipePlanDESnap.Resolve(cursor, drawCps, PipePlanDESnap.IsCtrlHeld(), PipePlanDESnap.GetSnapTolerance(editor));
                List<Point3d> fc = BuildFinal(snapped, out List<double> fr);
                return session.BuildRawCandidate(fc, fr);
            }))
            {
                string radLabel = manualRadius is double m ? $"R={m:0.###}" : $"R={tableRmin:0.###}(std)";
                PromptPointOptions options = new($"\nFortsæt: næste punkt [{radLabel}] [Radius/Default] (Ctrl=snap), Enter afslutter: ")
                {
                    BasePoint = drawCps[^1],
                    UseBasePoint = true,
                    AllowNone = true
                };
                options.Keywords.Add("Radius");
                options.Keywords.Add("Default");
                next = editor.GetPoint(options);
            }

            if (next.Status == PromptStatus.Keyword)
            {
                if (string.Equals(next.StringResult, "Radius", StringComparison.OrdinalIgnoreCase))
                {
                    PromptContinueRadius();
                }
                else if (string.Equals(next.StringResult, "Default", StringComparison.OrdinalIgnoreCase))
                {
                    manualRadius = null;
                    ReportDE(editor, $"Standard-bukkeradius gendannet (DN {session.Dn}: {tableRmin:0.###} m).");
                }

                continue;
            }

            if (next.Status == PromptStatus.None)
            {
                break;
            }

            if (next.Status != PromptStatus.OK)
            {
                session.ClearPreview();
                ReportDE(editor, "Fortsæt annulleret.");
                return;
            }

            (Point3d committed, _) = PipePlanDESnap.Resolve(next.Value, drawCps, PipePlanDESnap.IsCtrlHeld(), PipePlanDESnap.GetSnapTolerance(editor));
            drawCps.Add(committed);
            drawRadii.Add(0.0);
            if (drawCps.Count >= 3)
            {
                drawRadii[drawCps.Count - 2] = Effective();
            }

            session.ShowCandidatePreview(session.BuildRawCandidate(BuildFinal(null, out List<double> fr2), fr2));
            editor.UpdateScreen();
        }

        if (drawCps.Count <= originalCount)
        {
            session.ClearPreview();
            ReportDE(editor, "Ingen nye punkter tilføjet.");
            return;
        }

        List<Point3d> finalCps = BuildFinal(null, out List<double> finalRad);
        PipePlanDEEditCandidate candidate = session.BuildRawCandidate(finalCps, finalRad);
        if (!candidate.Feasible)
        {
            session.ClearPreview();
            ReportDE(editor, $"Kan ikke fortsætte: {candidate.Message}");
            return;
        }

        if (session.Commit(candidate, out string error))
        {
            session.ClearPreview();
            ReportDE(editor, $"Løb forlænget ({drawCps.Count - originalCount} nye hjørner).");
        }
        else
        {
            ReportDE(editor, $"Kunne ikke gemme: {error}");
        }
    }

    private static void ExecuteAnnotateDE(Document document)
    {
        Editor editor = document.Editor;
        Database db = document.Database;

        // Select the centrelines directly; the [Settings] keyword (S) opens the style/offset
        // dialog mid-prompt and then re-prompts for selection.
        SelectionFilter filter = new([new TypedValue((int)DxfCode.Start, "LWPOLYLINE")]);
        bool settingsRequested = false;
        PromptSelectionResult selection;
        while (true)
        {
            settingsRequested = false;
            PromptSelectionOptions selectionOptions = new()
            {
                MessageForAdding = "\nVælg akse-polylinjer (0-Centerline) at annotere eller [Settings]: "
            };
            selectionOptions.Keywords.Add("Settings");
            selectionOptions.KeywordInput += (_, e) =>
            {
                if (string.Equals(e.Input, "Settings", StringComparison.OrdinalIgnoreCase))
                {
                    settingsRequested = true;
                }
            };

            selection = editor.GetSelection(selectionOptions, filter);

            if (settingsRequested || selection.Status == PromptStatus.Keyword)
            {
                PipePlanDEAnnotationSettingsDialog.Show(db);
                continue;
            }

            if (selection.Status != PromptStatus.OK)
            {
                ReportDE(editor, "PDANNOTATE annulleret.");
                return;
            }

            break;
        }

        PipePlanDEAnnotationSettings settings = PipePlanDEAnnotationStore.Get(db);

        using DocumentLock documentLock = document.LockDocument();
        using Transaction transaction = db.TransactionManager.StartTransaction();
        try
        {
            // Gather valid PDDRAW centrelines (the drawn, filleted polylines).
            List<(Polyline Drawn, double Elevation)> centrelines = new();
            int skipped = 0;
            foreach (ObjectId id in selection.Value.GetObjectIds())
            {
                if (transaction.GetObject(id, OpenMode.ForRead) is not Polyline polyline)
                {
                    skipped++;
                    continue;
                }

                if (!PipePlanDEMetadata.TryRead(polyline, transaction, out PipePlanDEStoredData? data)
                    || data is null || data.Role != PipePlanDERole.Centerline || polyline.NumberOfVertices < 2)
                {
                    skipped++;
                    continue;
                }

                centrelines.Add((polyline, polyline.Elevation));
            }

            if (centrelines.Count == 0)
            {
                transaction.Commit();
                ReportDE(editor, "Ingen gyldige akse-polylinjer valgt (vælg 0-Centerline).");
                return;
            }

            // Resolve the dimension style (empty/unknown → current DIMSTYLE).
            ObjectId dimStyleId = db.Dimstyle;
            DimStyleTable dimStyleTable = (DimStyleTable)transaction.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            if (!string.IsNullOrEmpty(settings.StyleName) && dimStyleTable.Has(settings.StyleName))
            {
                dimStyleId = dimStyleTable[settings.StyleName];
            }

            // Dimension each centreline's straight (tangent-to-tangent) segments, split where
            // another selected centreline's drawn geometry crosses them, plus an arc-length dim
            // per fillet. Crossings are found on the real drawn polylines (arc-aware).
            int total = 0;
            for (int i = 0; i < centrelines.Count; i++)
            {
                Polyline drawn = centrelines[i].Drawn;

                List<Point3d> crossings = new();
                for (int j = 0; j < centrelines.Count; j++)
                {
                    if (i != j)
                    {
                        crossings.AddRange(drawn.IntersectWithValidation(centrelines[j].Drawn));
                    }
                }

                List<(Point3d Start, Point3d End)> straights = ExtractStraightSegments(drawn);
                List<(Point3d Start, Point3d End)> spans = PipePlanDEAnnotationGeometry.SplitSegments(straights, crossings);
                List<PipePlanDEArcDim> arcs = ExtractArcDims(drawn, crossings, centrelines[i].Elevation);

                total += PipePlanDEAnnotationWriter.Write(db, transaction, spans, arcs, settings.Offset, dimStyleId, centrelines[i].Elevation);
            }

            transaction.Commit();
            editor.Regen();
            string skippedNote = skipped > 0 ? $" ({skipped} sprunget over)" : string.Empty;
            ReportDE(editor, $"Annoteret {centrelines.Count} akse(r){skippedNote}: {total} mål på laget {PipePlanDEAnnotationWriter.AnnotationLayer}.");
        }
        catch
        {
            transaction.Abort();
            throw;
        }
    }

    // The straight (line) segments of the drawn centreline, tangent-to-tangent — each becomes an
    // aligned dimension. Arc segments are skipped here (they get arc-length dims instead).
    private static List<(Point3d Start, Point3d End)> ExtractStraightSegments(Polyline drawn)
    {
        List<(Point3d Start, Point3d End)> segments = new();
        int vertexCount = drawn.NumberOfVertices;
        int segmentCount = drawn.Closed ? vertexCount : vertexCount - 1;
        for (int i = 0; i < segmentCount; i++)
        {
            if (drawn.GetSegmentType(i) == SegmentType.Line)
            {
                segments.Add((drawn.GetPoint3dAt(i), drawn.GetPoint3dAt((i + 1) % vertexCount)));
            }
        }

        return segments;
    }

    // A DIMARC arc-length dimension per fillet arc, each split into sub-arcs at any crossing
    // that lies on it — so where two centrelines cross at their fillets, all four arc pieces are
    // annotated separately. Pure trig (angle sweep from the arc start) to avoid the CircularArc2d
    // parameter APIs.
    private static List<PipePlanDEArcDim> ExtractArcDims(Polyline drawn, IReadOnlyList<Point3d> crossings, double elevation)
    {
        const double onArcTol = 0.05; // XY distance for a crossing to count as "on the arc"
        const double angleEps = 1e-4;

        List<PipePlanDEArcDim> arcs = new();
        int vertexCount = drawn.NumberOfVertices;
        int segmentCount = drawn.Closed ? vertexCount : vertexCount - 1;

        for (int i = 0; i < segmentCount; i++)
        {
            if (drawn.GetSegmentType(i) != SegmentType.Arc)
            {
                continue;
            }

            CircularArc2d arc = drawn.GetArcSegment2dAt(i);
            Point2d c = arc.Center;
            double r = arc.Radius;
            bool ccw = drawn.GetBulgeAt(i) > 0.0;

            double startAngle = Math.Atan2(arc.StartPoint.Y - c.Y, arc.StartPoint.X - c.X);
            double endAngle = Math.Atan2(arc.EndPoint.Y - c.Y, arc.EndPoint.X - c.X);
            double totalSweep = ccw ? Norm2Pi(endAngle - startAngle) : Norm2Pi(startAngle - endAngle);

            // Sweep amounts (from the arc start) of crossings that lie on this arc.
            List<double> cuts = new();
            foreach (Point3d x in crossings)
            {
                double dx = x.X - c.X;
                double dy = x.Y - c.Y;
                if (Math.Abs(Math.Sqrt((dx * dx) + (dy * dy)) - r) > onArcTol)
                {
                    continue;
                }

                double angle = Math.Atan2(dy, dx);
                double sweep = ccw ? Norm2Pi(angle - startAngle) : Norm2Pi(startAngle - angle);
                if (sweep > angleEps && sweep < totalSweep - angleEps)
                {
                    cuts.Add(sweep);
                }
            }

            cuts.Sort();
            List<double> sweeps = [0.0, .. cuts, totalSweep];

            for (int k = 0; k < sweeps.Count - 1; k++)
            {
                double s0 = sweeps[k];
                double s1 = sweeps[k + 1];
                if (s1 - s0 < angleEps)
                {
                    continue;
                }

                Point3d start = PointOnArc(c, r, startAngle, ccw, s0, elevation);
                Point3d end = PointOnArc(c, r, startAngle, ccw, s1, elevation);
                Point3d mid = PointOnArc(c, r, startAngle, ccw, (s0 + s1) / 2.0, elevation);
                arcs.Add(new PipePlanDEArcDim(new Point3d(c.X, c.Y, elevation), start, end, mid, r));
            }
        }

        return arcs;
    }

    private static Point3d PointOnArc(Point2d center, double radius, double startAngle, bool ccw, double sweep, double elevation)
    {
        double angle = ccw ? startAngle + sweep : startAngle - sweep;
        return new Point3d(center.X + (radius * Math.Cos(angle)), center.Y + (radius * Math.Sin(angle)), elevation);
    }

    private static double Norm2Pi(double angle)
    {
        double twoPi = 2.0 * Math.PI;
        angle %= twoPi;
        return angle < 0.0 ? angle + twoPi : angle;
    }

    private static void HandleDECommandException(Document document, string commandName, System.Exception exception)
    {
        prdDbg(exception);
        document.Editor.WriteMessage($"\n{commandName} failed: {exception.Message}");
    }
}
