using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.MPE.MatchBBR;
using static IntersectUtilities.UtilsCommon.Utils;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>MATCHBBR</command>
        /// <summary>
        /// Opens the BBR match palette, comparing an Excel workbook against the BBR blocks inside closed boundary
        /// polylines. Tab "Excel" picks the workbook and declares the column-to-property comparison rules; tab
        /// "Sammenlign" picks the boundary and shows the full outer join (Match, Afvigelse, Kun i Excel, Kun i tegning,
        /// Dublet), which can be previewed, written back into the blocks and exported. Replaces the old MATCHBBR and
        /// COMPAREBBR commands.
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod(MatchBbrConstants.CommandName, CommandFlags.Modal)]
        public void matchbbr()
        {
            Document? document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return;
            }

            try
            {
                MatchBbrState state = MatchBbrRuntime.StateFor(document);
                MatchBbrRuntime.Palette.RebindTo(state);
                MatchBbrRuntime.Palette.Show();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                document.Editor.WriteMessage(
                    $"\n{MatchBbrConstants.CommandName} kunne ikke åbnes. Se debug-output.");
            }
        }
    }
}
