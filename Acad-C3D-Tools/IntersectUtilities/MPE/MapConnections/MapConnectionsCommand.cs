using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.MPE.MapConnections;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>MAPCONNECTIONS</command>
    /// <summary>
    /// Opens a palette mapping how the longitudinal profiles in the drawing connect. "Graf" shows the connectivity
    /// as a tree, "Net" draws the alignments as they lie in plan. Click an LP to zoom to its profile view, or a
    /// station chip to jump there as FWD does. Press "Opdater" after the profile views have been updated.
    /// </summary>
    /// <category>Længdeprofiler</category>
    [CommandMethod("MAPCONNECTIONS")]
    public void MapConnections()
    {
        Document? document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            MapConnectionsRuntime.Palette.Show();
            MapConnectionsRuntime.Refresh(document);
        }
        catch (System.Exception exception)
        {
            prdDbg(exception);
            document.Editor.WriteMessage($"\nMAPCONNECTIONS fejlede: {exception.Message}");
        }
    }
}
