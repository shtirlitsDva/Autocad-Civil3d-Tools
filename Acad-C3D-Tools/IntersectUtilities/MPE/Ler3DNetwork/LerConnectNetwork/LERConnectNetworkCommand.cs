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
        /// Opens a palette that lifts flat 2D drainage polylines (Afløbsledning layers, Z = -99) onto their nearest 3D
        /// main line. Each 2D line is assigned to a colour-coded network within a check distance; "Anvend tilslutninger"
        /// rebuilds it as a 3D polyline sloping upward from the connection point at the given per-mille. Lines with no
        /// main in range are left untouched and reported.
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
