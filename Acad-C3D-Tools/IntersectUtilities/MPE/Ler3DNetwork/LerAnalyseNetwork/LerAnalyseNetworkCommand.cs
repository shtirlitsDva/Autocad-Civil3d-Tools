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
        /// Opens a palette that finds flat 2D drainage chains (Afløbsledning layers, Z = -99) bridging the 3D pipe
        /// network and lifts them to real elevations. Bridges, floating chains and out-of-range chains are colour-coded
        /// in a preview; "Fiks alle broer" and "Fiks øer" rewrite the chosen chains as 3D polylines interpolated
        /// between the network elevations at each end.
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
