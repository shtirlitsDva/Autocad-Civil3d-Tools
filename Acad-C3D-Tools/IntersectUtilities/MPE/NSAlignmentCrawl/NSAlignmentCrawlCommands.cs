using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.MPE.Ler3DNetwork;
using IntersectUtilities.MPE.NSAlignmentCrawl;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

using CadColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>NSALIGNMENTCRAWL</command>
    /// <summary>
    /// Prototype alignment crawler. Reads the FJV network (pipes + component blocks) from the
    /// FV_Fremtid xref. A yellow X follows the cursor (snapped to the network) while you pick the
    /// start; then you pick the end with a live preview of the shortest crawl path, which follows the
    /// pipes and jumps through the blocks (port → block centre → port). Once the end is placed you
    /// confirm the direction with a high-visibility arrow + start-X overlay (Flip reverses, Enter
    /// accepts), then a single polyline following the crawl path is baked on layer 0 in the confirmed
    /// direction — ready to be turned into a Civil 3D alignment with "Create Alignment from Objects".
    /// </summary>
    /// <category>NSAlignmentCrawl</category>
    [CommandMethod("NSALIGNMENTCRAWL")]
    public void NSAlignmentCrawl()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteCrawl(document);
        }
        catch (System.Exception exception)
        {
            HandleCommandException(document, "NSALIGNMENTCRAWL", exception);
        }
    }

    private static void ExecuteCrawl(Document document)
    {
        Editor editor = document.Editor;
        Database db = document.Database;

        // 1. Read the network out of the FV_Fremtid xref into transaction-free POCOs.
        NSAlignmentCrawlSnapshot snapshot;
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            bool ok = NSAlignmentCrawlNetworkReader.TryRead(
                db, tr, NSAlignmentCrawlConstants.XrefName, out snapshot, out string readError);
            tr.Commit();
            if (!ok)
            {
                editor.WriteMessage($"\n{readError}");
                return;
            }
        }

        if (snapshot.Pipes.Count == 0)
        {
            editor.WriteMessage("\nIngen FJV-rør fundet i xref'en.");
            return;
        }

        using CrawlNetwork net = NSAlignmentCrawlGraphBuilder.Build(snapshot);

        // 2. Pick the start point. A yellow X follows the cursor, snapped to the network, so you can
        //    see where station 0 will land on the xref pipes/blocks before committing.
        PromptPointResult startResult;
        using (NSAlignmentCrawlStartMarker startCursor = new(CadColor.FromRgb(255, 255, 0)))
        {
            void OnStartMove(object? sender, PointMonitorEventArgs args)
            {
                try
                {
                    if (CrawlSession.TrySnapToNetwork(net, args.Context.ComputedPoint, out Point2d snapped))
                    {
                        startCursor.Show(document, snapped);
                    }
                    else
                    {
                        startCursor.Clear();
                    }
                }
                catch
                {
                    // A PointMonitor handler must never throw — a bad tick simply shows no marker.
                }
            }

            editor.PointMonitor += OnStartMove;
            try
            {
                startResult = editor.GetPoint(
                    new PromptPointOptions("\nVælg startpunkt på røret (X følger nettet): "));
            }
            finally
            {
                editor.PointMonitor -= OnStartMove;
            }
        }

        if (startResult.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\nAnnulleret.");
            return;
        }

        CrawlSession? session = CrawlSession.Create(net, startResult.Value, out string startError);
        if (session is null)
        {
            editor.WriteMessage($"\n{startError}");
            return;
        }

        // 3. Pick the end point with a live preview of the crawl path; the start X stays put.
        List<(Point2d Pt, double OutBulge)>? finalVertices = null;

        List<(Point2d Pt, double OutBulge)>? BuildPreview(Point3d cursor)
            => session.TryBuildPath(cursor, out List<(Point2d Pt, double OutBulge)> seg) && seg.Count >= 2
                ? seg
                : null;

        using (NSAlignmentCrawlPreviewManager preview = new())
        using (NSAlignmentCrawlStartMarker startMarker = new(CadColor.FromRgb(255, 255, 0)))
        using (new NSAlignmentCrawlPointTracker(document, preview, BuildPreview))
        {
            startMarker.Show(document, session.StartPosition);

            PromptPointResult endResult = editor.GetPoint(
                new PromptPointOptions("\nVælg slutpunkt (forhåndsvisning følger markøren): "));
            if (endResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nAnnulleret.");
                return;
            }

            if (session.TryBuildPath(endResult.Value, out List<(Point2d Pt, double OutBulge)> vertices))
            {
                finalVertices = vertices;
            }
        }

        if (finalVertices is null)
        {
            editor.WriteMessage("\nIngen sti fundet mellem de to punkter.");
            return;
        }

        // 4. Confirm direction, then bake the crawl polyline.
        TransformToAlignment(document, editor, db, finalVertices);
    }

    /// <summary>
    /// Direction confirmation (arrow + start-X overlay; Flip reverses, Enter accepts), then bake the
    /// path polyline on layer 0 in the confirmed direction. The polyline is baked in the order the
    /// user confirmed, so its vertex order (and thus a later "Create Alignment from Objects") starts
    /// at the X — the arrows are the real direction.
    /// </summary>
    private static void TransformToAlignment(
        Document document, Editor editor, Database db, List<(Point2d Pt, double OutBulge)> finalVertices)
    {
        List<(Point2d Pt, double OutBulge)> verts = finalVertices;

        using (NSAlignmentCrawlPreviewManager pathPreview = new())
        using (NSAlignmentCrawlStartMarker startMarker = new(CadColor.FromRgb(255, 255, 0)))
        using (LerSlopeArrowManager arrows = new(CadColor.FromRgb(0, 255, 0)))
        {
            // TODO: the direction situation still needs a proper fix. Flip only reorders the baked
            // polyline's vertices, and we rely on "Create Alignment from Objects" (run manually for
            // now) honouring that vertex order for station 0 / direction. Decide how the alignment
            // direction should ultimately be owned and enforced instead of leaving it implicit in the
            // polyline order.
            //
            // Direction confirmation: arrows show travel direction, the X marks the start (station 0).
            // Flip reverses (repeatable), Enter accepts the shown direction.
            while (true)
            {
                ShowPath(pathPreview, verts);
                startMarker.Show(document, verts[0].Pt);
                arrows.Show(document, BuildArrowAnchors(verts));

                PromptKeywordOptions dir = new(
                    "\nBekræft alignment-retning — [Flip] vender retningen, Enter accepterer");
                dir.Keywords.Add("Flip");
                dir.AllowNone = true; // Enter = accept the shown direction
                PromptResult res = editor.GetKeywords(dir);
                if (res.Status == PromptStatus.None)
                {
                    break; // accepted
                }

                if (res.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\nAnnulleret.");
                    return;
                }

                if (res.StringResult == "Flip")
                {
                    verts = ReversePath(verts);
                }
            }
        }

        // Bake the confirmed path on layer 0 in the host drawing.
        using (DocumentLock documentLock = document.LockDocument())
        using (Transaction tx = db.TransactionManager.StartTransaction())
        {
            Polyline? polyline = NSAlignmentCrawlPolylineBuilder.Build(verts, NSAlignmentCrawlConstants.OutputLayer);
            if (polyline is null)
            {
                tx.Abort();
                editor.WriteMessage("\nFor få punkter til at tegne.");
                return;
            }

            BlockTableRecord modelSpace = db.GetModelspaceForWrite();
            modelSpace.AppendEntity(polyline);
            tx.AddNewlyCreatedDBObject(polyline, add: true);
            double length = polyline.Length;
            tx.Commit();

            editor.WriteMessage(
                $"\nCrawl-polylinje tegnet på lag {NSAlignmentCrawlConstants.OutputLayer} ({length:0.###} m).");
        }
    }

    private static void ShowPath(NSAlignmentCrawlPreviewManager preview, IReadOnlyList<(Point2d Pt, double OutBulge)> verts)
    {
        Polyline? polyline = NSAlignmentCrawlPolylineBuilder.Build(verts, NSAlignmentCrawlConstants.OutputLayer);
        if (polyline is not null)
        {
            preview.Show(polyline);
        }
    }

    /// <summary>
    /// Samples direction arrowheads along the path: a handful of evenly spaced anchors, each with the
    /// unit tangent in travel direction, for <see cref="LerSlopeArrowManager"/> to render.
    /// </summary>
    private static List<LerSlopeAnchor> BuildArrowAnchors(IReadOnlyList<(Point2d Pt, double OutBulge)> verts)
    {
        List<LerSlopeAnchor> anchors = [];
        Polyline? pl = NSAlignmentCrawlPolylineBuilder.Build(verts, NSAlignmentCrawlConstants.OutputLayer);
        if (pl is null)
        {
            return anchors;
        }

        try
        {
            double length = pl.Length;
            if (length <= 1e-6)
            {
                return anchors;
            }

            int count = Math.Clamp((int)Math.Round(length / 10.0), 3, 15);
            for (int i = 0; i < count; i++)
            {
                double dist = (i + 0.5) / count * length; // mid-spaced — stays off vertices
                try
                {
                    Point3d p = pl.GetPointAtDist(dist);
                    Vector3d t = pl.GetFirstDerivative(pl.GetParameterAtPoint(p));
                    double tl = t.Length;
                    if (tl <= 1e-9)
                    {
                        continue;
                    }

                    anchors.Add(new LerSlopeAnchor(p.X, p.Y, 0.0, t.X / tl, t.Y / tl));
                }
                catch
                {
                    // Skip a sample that lands on a degenerate spot; the rest still convey direction.
                }
            }
        }
        finally
        {
            pl.Dispose();
        }

        return anchors;
    }

    /// <summary>Reverses the path, negating arc bulges so curves stay correct (reuses the builder).</summary>
    private static List<(Point2d Pt, double OutBulge)> ReversePath(IReadOnlyList<(Point2d Pt, double OutBulge)> verts)
    {
        using Polyline pl = NSAlignmentCrawlPolylineBuilder.Build(verts, NSAlignmentCrawlConstants.OutputLayer)!;
        return NSAlignmentCrawlPolylineBuilder.ReadReversed(pl);
    }
}
