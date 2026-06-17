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
    /// <summary>Shows the PipePlanDE palette: pick the active DN to draw and edit the per-DN parameter table (z1, d, x, … b, B, B1). Overrides are saved to the active drawing.</summary>
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
            PipePlanDERuntime.Palette.RebindTo(state);
            PipePlanDERuntime.Palette.Show();
            PipePlanDERuntime.Palette.SetStatus("Vælg dimension og kør PDDRAW.", PipePlanStatusKind.Info);
        }
        catch (System.Exception exception)
        {
            HandleDECommandException(document, "PDSETTINGS", exception);
        }
    }

    /// <command>PDDRAW</command>
    /// <summary>Draws a single-pipe run. Uses the active DN from the PDSETTINGS palette: you draw the routing centreline interactively, and on Enter the centreline (layer 0-Centerline) plus two mantle-OD-wide frem/retur polylines are placed in the drawing with sharp mitered corners (no bending radius).</summary>
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
            ReportDE(editor, "Vælg en dimension i PDSETTINGS-paletten først.");
            PipePlanDERuntime.Palette.RebindTo(state);
            PipePlanDERuntime.Palette.Show();
            PipePlanDERuntime.Palette.SetStatus("Vælg en dimension for at tegne.", PipePlanStatusKind.Warning);
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

        BakeDE(document, dn, parameters, points, flip);
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

    private static void BakeDE(Document document, int dn, PipePlanDEParameters parameters, IReadOnlyList<Point3d> points, bool flip)
    {
        Editor editor = document.Editor;

        using DocumentLock documentLock = document.LockDocument();
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            if (!PipePlanDEPolylineWriter.TryWrite(document.Database, transaction, points, dn, parameters, flip, out string writeError))
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

            // parameters[8] = B (Regelgrabenbreite), the total trench width.
            if (!PipePlanDETrenchWriter.TryWrite(document.Database, transaction, axisPoints, parameters[8], out string writeError))
            {
                transaction.Abort();
                ReportDE(editor, $"Kunne ikke tegne grav: {writeError}");
                return;
            }

            transaction.Commit();
            // A freshly created hatch often doesn't display its fill until a regen.
            editor.Regen();
            ReportDE(editor, $"Grav tegnet for DN {data.Dn} (B={parameters[8]:0.##} m) på laget {PipePlanDETrenchWriter.TrenchLayer}.");
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
