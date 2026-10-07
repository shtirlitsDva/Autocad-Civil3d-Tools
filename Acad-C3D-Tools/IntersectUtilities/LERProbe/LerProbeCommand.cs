using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.LerPathCrawl;
using IntersectUtilities.LerProbe;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>LERPROBE</command>
    /// <summary>
    /// Select a polyline inside an xref and inspect its attached AEC property
    /// sets in a searchable, read-only modeless table. Draws the complete selected polyline
    /// in bright red (blue for a red pipe) while the window is open. Pan/zoom remain available.
    /// </summary>
    /// <category>LER</category>
    [CommandMethod("LERPROBE", CommandFlags.Modal)]
    public void LerProbe()
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        var ed = document.Editor;
        try
        {
            var pick = ed.GetNestedEntity(new PromptNestedEntityOptions("\nSelect a LER polyline inside an xref to inspect: "));
            if (pick.Status != PromptStatus.OK)
                return;

            LerCrawlResult<LerProbeSnapshot> result;
            LerCrawlResult<LerProbeGraphic> graphic;
            using (var hostRead = document.Database.TransactionManager.StartTransaction())
            {
                result = LerProbeReader.Read(hostRead, pick.ObjectId, pick.GetContainers());
                graphic = result.Match(
                    _ => LerProbeHighlight.Resolve(hostRead, pick.ObjectId, pick.GetContainers()).Match(
                        path => LerProbeGraphicBuilder.Read(hostRead, document.Database, path, pick.Transform),
                        error => LerCrawlResult<LerProbeGraphic>.Fault(error)),
                    error => LerCrawlResult<LerProbeGraphic>.Fault(error));
            }

            // The window owns detached preview geometry and data, with no open
            // transaction or document lock after this command returns.
            result.Match(
                snapshot =>
                {
                    LerProbeSession.Open(document, snapshot, graphic);
                    return true;
                },
                error => { ed.WriteMessage($"\n{error}"); return false; });
        }
        catch (System.Exception ex)
        {
            UtilsCommon.Utils.prdDbg(ex);
            ed.WriteMessage($"\nUnable to complete LERPROBE: {ex.Message}");
        }
    }
}
