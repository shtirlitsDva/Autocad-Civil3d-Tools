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
        /// Opens the BBR match palette, which compares an Excel workbook against the BBR blocks
        /// inside one or more closed boundary polylines. Replaces the previous MATCHBBR and
        /// COMPAREBBR commands, which each answered only one half of the question and could
        /// disagree with each other about the same address.
        ///
        /// Tab "Excel" selects the workbook and worksheet and declares the comparison rules. Each
        /// rule joins any number of Excel columns (Vejnavn + Husnummer, or Vejnavn + Husnummer +
        /// Etage + Dør, or a single Adresse column) and maps them to one BBR property set property
        /// with a match rule; exactly one rule is marked as the key the two sides are joined on.
        /// The row is split visually, Excel on the left of the divider and BBR on the right. Rules
        /// seed themselves from the sheet headers and are remembered per workbook. A filter column,
        /// defaulting to Varmedistrikt, narrows the rows to the district being worked on.
        ///
        /// Tab "Sammenlign" picks the boundary, reads the BBR blocks inside it and shows the full
        /// outer join: Match, Afvigelse (address matched but a compared value differs), Kun i
        /// Excel, Kun i tegning, and Dublet. Selecting a row zooms to its block. From there the
        /// result can be previewed in the drawing, written back into the BBR blocks either in bulk
        /// or one value at a time, and exported to a workbook.
        ///
        /// The preview is transient graphics only — circles coloured by outcome around whatever
        /// rows the grid is currently showing. Nothing is written to the drawing, and the circles
        /// disappear when the preview is toggled off, the palette is closed, or the session ends.
        ///
        /// Boundary polylines carrying arc segments are rejected, because the containment test is
        /// a straight-segment ray cast. Blocks whose BBR Type is "Ingen" are skipped by default.
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
