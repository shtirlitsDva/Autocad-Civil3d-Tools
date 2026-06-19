using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.MPE.NSAlignmentCrawl;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>NSALIGNMENTCRAWL</command>
    /// <summary>
    /// Prototype alignment crawler. Reads the FJV network (pipes + component blocks) from the
    /// FV_Fremtid xref, lets you pick a start and end point anywhere on the network, previews the
    /// shortest crawl path live, and bakes a single polyline on layer 0 that follows the pipes and
    /// jumps through the blocks (port → block centre → port). No Civil 3D alignment is created yet.
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

        // 2. Pick the start point; this fixes the source of the single-source Dijkstra.
        PromptPointResult startResult = editor.GetPoint(new PromptPointOptions("\nVælg startpunkt på røret: "));
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

        // 3. Pick the end point with a live preview of the crawl path.
        List<(Point2d Pt, double OutBulge)>? finalVertices = null;
        using (NSAlignmentCrawlPreviewManager preview = new())
        using (new NSAlignmentCrawlPointTracker(document, preview, session))
        {
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

        // 4. Bake the resulting polyline on layer 0 in the host drawing.
        using DocumentLock documentLock = document.LockDocument();
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            Polyline? polyline = NSAlignmentCrawlPolylineBuilder.Build(finalVertices, NSAlignmentCrawlConstants.OutputLayer);
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
        catch
        {
            tx.Abort();
            throw;
        }
    }
}
