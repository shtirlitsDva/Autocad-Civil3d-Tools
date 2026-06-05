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
        /// Opens a palette that analyses, then lifts, flat 2D drainage chains (on layers whose name contains "Afløbsledning",
        /// vertices at Z = -99) that bridge the existing 3D pipe network. On "Indlæs og analyser" it gathers every Polyline3d,
        /// splits them into 3D (real elevations) and 2D, and finds the pivots — 2D endpoints where a 3D pipe's endpoint,
        /// projected to the plan (XY), lands within 1e-6 m. A multi-source scan out from the pivots classifies each 2D segment
        /// as a bridge (a simple path joining two distinct pivots through it), floating (reachable from only one pivot, or a
        /// dangling stub), or out of range; a scan-depth slider bounds how far from each pivot the scan reaches. Bridges
        /// preview green, floating amber/red, out-of-range hidden, with the 3D pivots and downhill slope arrows as optional
        /// context. "Fiks alle broer" (a two-click arm/confirm) and "Fiks øer" rewrite the chosen flat chains as real 3D
        /// polylines: each chain end snaps to the elevation of the 3D pipe it touches and every interior vertex interpolates
        /// linearly by cumulative plan length between the two ends, so the chain is tied into the network at both ends. Those
        /// two actions modify the drawing (the original 2D polylines are replaced); everything else is read-only preview.
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
