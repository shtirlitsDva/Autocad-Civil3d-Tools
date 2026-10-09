using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.LerHatchLayers;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>LERHATCHLAYERS</command>
    /// <summary>
    /// Assigns model-space LER component hatches on layer 0 to existing LER polyline layers
    /// using component property sets, operating status and electrical voltage. Sets their
    /// colour to ByLayer and reports missing or ambiguous mappings without changing them.
    /// Electrical components with missing or unknown voltage use EL-Elledning-Ukendt.
    /// </summary>
    /// <category>LER</category>
    [CommandMethod("LERHATCHLAYERS", CommandFlags.Modal)]
    public void LerHatchLayers() => RunLerHatchLayers(preview: false);

    /// <command>LERHATCHLAYERSPREVIEW</command>
    /// <summary>Reports the LERHATCHLAYERS assignments and skipped hatch handles without editing the drawing.</summary>
    /// <category>LER</category>
    [CommandMethod("LERHATCHLAYERSPREVIEW", CommandFlags.Modal)]
    public void LerHatchLayersPreview() => RunLerHatchLayers(preview: true);

    private static void RunLerHatchLayers(bool preview)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        Editor editor = document.Editor;
        try
        {
            using Transaction tx = document.Database.TransactionManager.StartTransaction();
            LerHatchLayerService.BuildPlan(document.Database, tx).Match(
                plan =>
                {
                    if (!preview)
                    {
                        LerHatchLayerService.Apply(tx, plan);
                        tx.Commit();
                    }
                    editor.WriteMessage($"\n{(preview ? "Preview" : "LERHATCHLAYERS")}: "
                        + $"{plan.Assignments.Count} hatches {(preview ? "would be moved" : "moved")}; {plan.Skipped.Count} skipped.");
                    foreach (var group in plan.Assignments.GroupBy(assignment => assignment.LayerName).OrderBy(group => group.Key))
                        editor.WriteMessage($"\n  {group.Key}: {group.Count()}");
                    foreach (var group in plan.Skipped.GroupBy(skip => skip.Reason).OrderBy(group => group.Key))
                        editor.WriteMessage($"\n  Skipped {group.Count()}: {group.Key}\n    Handles: "
                            + string.Join(", ", group.Select(skip => skip.Handle)));
                    return true;
                },
                reason => { editor.WriteMessage("\nLERHATCHLAYERS: " + reason); return false; });
            if (!preview) editor.Regen();
        }
        catch (System.Exception ex)
        {
            UtilsCommon.Utils.prdDbg(ex);
            editor.WriteMessage("\nLERHATCHLAYERS failed; see debug output. " + ex.Message);
        }
    }
}
