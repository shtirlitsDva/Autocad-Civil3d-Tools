using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork;
using static IntersectUtilities.UtilsCommon.Utils;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>LERANALYSENETWORK</command>
        /// <summary>
        /// Opens a palette that visualizes which flat 2D drainage segments (on layers whose name contains
        /// "Afløbsledning", vertices at Z = -99) are connected in projection to the existing 3D pipe network. On "Indlæs og
        /// analyser" it gathers every Polyline3d, splits them into 3D (real elevations) and 2D, and tests each 2D drainage
        /// segment: a segment is marked connected when any 3D pipe's endpoint — projected to the plan (XY) — lands on that
        /// segment within 1e-6 m. All 3D polylines in the drawing count as pipes, regardless of layer. Connected segments
        /// preview green, unconnected red, and the 3D pipes can be shown as grey context. The command is read-only — it never
        /// modifies the drawing.
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod("LERANALYSENETWORK", CommandFlags.Modal)]
        public void LERAnalyseNetwork()
        {
            Document? document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return;
            }

            try
            {
                LerAnalyseNetworkState state = LerAnalyseNetworkRuntime.StateFor(document);
                LerAnalyseNetworkRuntime.Palette.RebindTo(state);
                LerAnalyseNetworkRuntime.Palette.Show();
                // Gather is deferred to the "Indlæs og analyser" button so the
                // palette can open without reading the drawing on launch.
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                document.Editor.WriteMessage("\nLERANALYSENETWORK failed. See debug output for details.");
            }
        }
    }
}
