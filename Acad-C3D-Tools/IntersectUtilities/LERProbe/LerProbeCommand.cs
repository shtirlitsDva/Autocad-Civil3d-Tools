using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.LerProbe;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>LERPROBE</command>
    /// <summary>
    /// Select a polyline inside an xref and inspect its attached AEC property
    /// sets in a searchable, read-only table. Reads directly from the loaded source drawing.
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

            LerPathCrawl.LerCrawlResult<LerProbeSnapshot> result;
            using (var hostRead = document.Database.TransactionManager.StartTransaction())
                result = LerProbeReader.Read(hostRead, pick.ObjectId, pick.GetContainers());

            // The dialog holds only strings and values; all native reads have ended.
            result.Match(
                snapshot =>
                {
                    using var window = new LerProbeWindow(snapshot);
                    Application.ShowModalDialog(window);
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
