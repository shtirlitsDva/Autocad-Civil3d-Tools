using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.LerPathCrawl;
using IntersectUtilities.MPE.NSAlignmentCrawl;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>LERCRAWL</command>
    /// <summary>
    /// Select a start on a LER polyline in an xref, then an endpoint with a live shortest-route
    /// preview. Follows only the exact selected utility layer in that xref instance.
    /// Both point prompts support running object snaps and temporary snap overrides.
    /// Creates one zero-width plan centreline in host model space on 0-REFERENCELINE.
    /// </summary>
    /// <category>LER</category>
    [CommandMethod("LERCRAWL", CommandFlags.Modal | CommandFlags.NoPaperSpace | CommandFlags.UsePickSet)]
    public void LerPathCrawl()
    {
        Document document = Application.DocumentManager.MdiActiveDocument;
        Database db = document.Database;
        Editor ed = document.Editor;
        try
        {
            ed.WriteMessage("\nUse F3 to toggle object snaps, or Shift+right-click for a snap override.");
            var start = ed.GetPoint(new PromptPointOptions("\nSelect start point on a LER polyline in an xref: "));
            if (start.Status != PromptStatus.OK)
                return;

            // GetPoint provides normal OSNAP control. Resolve the nested entity at
            // the accepted point without asking for another click. Both APIs use UCS.
            Matrix3d ucs = ed.CurrentUserCoordinateSystem;
            var pick = ed.GetNestedEntity(new PromptNestedEntityOptions(string.Empty)
            {
                UseNonInteractivePickPoint = true,
                NonInteractivePickPoint = start.Value
            });
            if (pick.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nNo LER polyline was found at the start point. Pick on a polyline in the LER xref.");
                return;
            }

            LerCrawlResult<LerCrawlSource> source;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                source = LerCrawlReader.ReadSelection(tr, pick.ObjectId, pick.GetContainers(), start.Value, ucs);
                tr.Commit();
            }
            source.Match(
                value => { ExecuteLerCrawl(document, value); return true; },
                error => { ed.WriteMessage($"\n{error}"); return false; });
        }
        catch (System.Exception ex)
        {
            IntersectUtilities.UtilsCommon.Utils.prdDbg(ex);
            ed.WriteMessage($"\nUnable to complete LERCRAWL: {ex.Message}");
        }
    }

    private static void ExecuteLerCrawl(Document document, LerCrawlSource source)
    {
        Editor ed = document.Editor;
        ed.WriteMessage($"\nFollowing {source.SourceLayer} in {source.XrefName}.");
        using var graph = new LerCrawlGraph();
        graph.Read(source.Segments);
        LerCrawlSession.Create(graph, source.SelectedId, source.StartPick).Match(
            start => { PickLerCrawlEnd(document, start.Session); return true; },
            error => { ed.WriteMessage($"\n{error}"); return false; });
    }

    private static void PickLerCrawlEnd(Document document, LerCrawlSession session)
    {
        Editor ed = document.Editor;
        using var preview = new NSAlignmentCrawlPreviewManager();
        using var marker = new NSAlignmentCrawlStartMarker(Color.FromColorIndex(ColorMethod.ByAci, 2));
        marker.Show(document, session.Start);
        bool previewFailureLogged = false;
        void OnMove(object? sender, PointMonitorEventArgs args)
        {
            try
            {
                session.Route(args.Context.ComputedPoint).Match(
                    route => { preview.Show(LerCrawlPolylineBuilder.Centerline(route.Segments)); return true; },
                    _ => { preview.Clear(); return false; });
            }
            catch (System.Exception ex)
            {
                preview.Clear();
                if (!previewFailureLogged)
                {
                    previewFailureLogged = true;
                    IntersectUtilities.UtilsCommon.Utils.prdDbg(ex);
                }
            }
        }

        ed.PointMonitor += OnMove;
        PromptPointResult end;
        try
        {
            end = ed.GetPoint(new PromptPointOptions("\nSelect endpoint (red line previews the route): "));
        }
        finally
        {
            ed.PointMonitor -= OnMove;
            preview.Clear();
        }
        if (end.Status != PromptStatus.OK)
            return;

        session.Route(end.Value.TransformBy(ed.CurrentUserCoordinateSystem)).Match(
            route => { WriteLerCrawl(document, route); return true; },
            error => { ed.WriteMessage($"\n{error}"); return false; });
    }

    private static void WriteLerCrawl(Document document, LerCrawlRoute route)
    {
        Database db = document.Database;
        using var centerline = LerCrawlPolylineBuilder.Centerline(route.Segments);
        using var tr = db.TransactionManager.StartTransaction();
        try
        {
            ObjectId id = LerCrawlDrawingWriter.Append(db, tr, centerline);
            tr.Commit();
            document.Editor.WriteMessage($"\nCreated centreline ({centerline.Length:G8} drawing units) on {LerCrawlSettings.Layer}.");
            document.Editor.SetImpliedSelection(new[] { id });
        }
        catch
        {
            tr.Abort();
            throw; // The command boundary logs and reports the failure.
        }
    }
}
