using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork;
using static IntersectUtilities.UtilsCommon.Utils;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>LERCONNECTNETWORK</command>
        /// <summary>
        /// Opens a palette that lifts flat 2D drainage polylines (vertices at Z = -99) on layers whose name contains
        /// "Afløbsledning" onto their nearest 3D parent line. On open it gathers every such polyline and classifies it as
        /// 3D (real elevations) or 2D. A check distance groups the touching 3D lines into colour-coded networks and assigns
        /// each 2D line to the nearest network within that distance; the 3D/2D split and the parent↔child network can be
        /// previewed transiently. "Anvend tilslutninger" extends each connected 2D line's nearest end along its tangent to
        /// intersect the parent in XY, lifts that point to the parent's real elevation as a pivot, and rebuilds the line as
        /// a new 3D polyline sloping upward away from the pivot at the given per-mille. The original 2D line is erased and
        /// the rebuilt line keeps the source layer and properties. 2D lines with no parent within the distance, or whose
        /// tangent never meets the parent, are left untouched and reported. The 3D join is in-memory only — original 3D
        /// geometry is never modified.
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod("LERCONNECTNETWORK", CommandFlags.Modal)]
        public void LERConnectNetwork()
        {
            Document? document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return;
            }

            try
            {
                LERConnectNetworkState state = LERConnectNetworkRuntime.StateFor(document);
                LERConnectNetworkRuntime.Palette.RebindTo(state);
                LERConnectNetworkRuntime.Palette.Show();
                // Gather is deferred to the "Anvend" (apply distance) button so the
                // palette can open without reading the drawing on launch.
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                document.Editor.WriteMessage("\nLERCONNECTNETWORK failed. See debug output for details.");
            }
        }
    }
}
