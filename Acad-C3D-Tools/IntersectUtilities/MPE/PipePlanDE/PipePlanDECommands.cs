using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.MPE.PipePlan;
using IntersectUtilities.MPE.PipePlanDE;
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
    /// <summary>Opens the dimension picker and draws a single-pipe run. The window stays open to select the drawing size; if no DN is selected yet, PDDRAW just shows it and waits. With a DN selected, you draw the routing centreline interactively, and on Enter the centreline (layer 0-Centerline) plus two mantle-OD-wide frem/retur polylines are placed with sharp mitered corners (no bending radius). Closing the window and running PDDRAW reopens it.</summary>
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
    /// <summary>Click a PDDRAW centreline to draw its trench: a SOLID, 80%-transparent hatch on the "Gravearbejde" layer, sized to the stored DN's Regelgrabenbreite (B). The boundary is the axis buffered by B/2, so it stays clean at sharp bends.</summary>
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

        if (!TryCollectCenterline(document, dn, parameters, out List<Point3d> points, out bool flip))
        {
            return;
        }

        if (points.Count < 2)
        {
            ReportDE(editor, "Færre end to punkter — intet tegnet.");
            return;
        }

        BakeDE(document, dn, parameters, points, flip, state.ActiveDepth);
    }

    private static bool TryCollectCenterline(Document document, int dn, PipePlanDEParameters parameters, out List<Point3d> points, out bool flip)
    {
        Editor editor = document.Editor;
        points = [];
        flip = false;
        // Captured by the tracker lambda; mirrored back to the out-param on success.
        bool flipState = false;

        PromptPointResult first = editor.GetPoint(new PromptPointOptions($"\nDN {dn}: Første punkt på centerlinje: "));
        if (first.Status != PromptStatus.OK)
        {
            ReportDE(editor, "PDDRAW annulleret.");
            return false;
        }

        points.Add(first.Value);

        using PipePlanDEPreviewManager preview = new();
        while (true)
        {
            PromptPointResult next;
            using (new PipePlanDEPointTracker(document, preview, points, parameters, () => flipState))
            {
                PromptPointOptions options = new("\nNæste punkt, [Flip] frem/retur (hold Ctrl for lige/vinkelret snap), eller Enter for at afslutte: ")
                {
                    BasePoint = points[^1],
                    UseBasePoint = true,
                    AllowNone = true
                };
                options.Keywords.Add("Flip");

                next = editor.GetPoint(options);
            }

            if (next.Status == PromptStatus.Keyword)
            {
                if (string.Equals(next.StringResult, "Flip", StringComparison.OrdinalIgnoreCase))
                {
                    flipState = !flipState;
                    preview.Show(points, parameters, flipState);
                    editor.UpdateScreen();
                    ReportDE(editor, flipState ? "Frem/retur byttet." : "Frem/retur normal.");
                }

                continue;
            }

            if (next.Status == PromptStatus.None)
            {
                flip = flipState;
                return true;
            }

            if (next.Status != PromptStatus.OK)
            {
                ReportDE(editor, "PDDRAW annulleret.");
                points.Clear();
                return false;
            }

            (Point3d committed, _) = PipePlanDESnap.Resolve(
                next.Value, points, PipePlanDESnap.IsCtrlHeld(), PipePlanDESnap.GetSnapTolerance(editor));
            points.Add(committed);
            preview.Show(points, parameters, flipState);
            editor.UpdateScreen();
        }
    }

    private static void BakeDE(Document document, int dn, PipePlanDEParameters parameters, IReadOnlyList<Point3d> points, bool flip, PipePlanDETrenchDepth depth)
    {
        Editor editor = document.Editor;

        using DocumentLock documentLock = document.LockDocument();
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            if (!PipePlanDEPolylineWriter.TryWrite(document.Database, transaction, points, dn, parameters, flip, depth, out string writeError))
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

        ReportDE(editor, $"Tegnet DN {dn}: centerlinje + frem/retur ({points.Count} hjørner).");
    }

    private static void ExecuteTrenchDE(Document document)
    {
        Editor editor = document.Editor;

        PromptEntityOptions options = new("\nKlik på akse-polylinjen (0-Centerline): ");
        options.SetRejectMessage("\nKun polylinjer understøttes.");
        options.AddAllowedClass(typeof(Polyline), exactMatch: false);

        PromptEntityResult pick = editor.GetEntity(options);
        if (pick.Status != PromptStatus.OK)
        {
            ReportDE(editor, "PDTRENCH annulleret.");
            return;
        }

        using DocumentLock documentLock = document.LockDocument();
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(pick.ObjectId, OpenMode.ForRead);
            if (!PipePlanDEMetadata.TryRead(polyline, transaction, out PipePlanDEStoredData? data) || data is null)
            {
                transaction.Commit();
                ReportDE(editor, "Ikke en PipePlanDE-polylinje (ingen DN-metadata).");
                return;
            }

            if (data.Role != PipePlanDERole.Centerline)
            {
                transaction.Commit();
                ReportDE(editor, "Vælg akse-polylinjen (0-Centerline), ikke et rør.");
                return;
            }

            PipePlanDEParameters? parameters = PipePlanDEParameterStore.GetEffective(document.Database, data.Dn);
            if (parameters is null)
            {
                transaction.Commit();
                ReportDE(editor, $"DN {data.Dn} — ingen parametre fundet.");
                return;
            }

            List<Point3d> axisPoints = ReadAxisVertices(polyline);

            // Depth (baked at PDDRAW time) picks the Regelgrabenbreite: ≤ 1.3 m → B,
            // > 1.3 m → the wider B1.
            bool deep = data.Depth == PipePlanDETrenchDepth.Deep;
            double trenchWidth = deep ? parameters.B1 : parameters.B;
            string widthLabel = deep ? "B1" : "B";

            if (!PipePlanDETrenchWriter.TryWrite(document.Database, transaction, axisPoints, trenchWidth, out string writeError))
            {
                transaction.Abort();
                ReportDE(editor, $"Kunne ikke tegne grav: {writeError}");
                return;
            }

            transaction.Commit();
            // A freshly created hatch often doesn't display its fill until a regen.
            editor.Regen();
            ReportDE(editor, $"Grav tegnet for DN {data.Dn} ({widthLabel}={trenchWidth:0.##} m, dybde {(deep ? "> 1,3 m" : "≤ 1,3 m")}) på laget {PipePlanDETrenchWriter.TrenchLayer}.");
        }
        catch
        {
            transaction.Abort();
            throw;
        }
    }

    private static List<Point3d> ReadAxisVertices(Polyline polyline)
    {
        List<Point3d> points = new(polyline.NumberOfVertices);
        for (int i = 0; i < polyline.NumberOfVertices; i++)
        {
            points.Add(polyline.GetPoint3dAt(i));
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

    private static void HandleDECommandException(Document document, string commandName, System.Exception exception)
    {
        prdDbg(exception);
        document.Editor.WriteMessage($"\n{commandName} failed: {exception.Message}");
    }
}
