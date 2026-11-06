using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.LineTypes;

using Norsyn.DrawingTools.Linetypes;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    // The linetype commands' heads. Their bodies are IntersectUtilitiesCOMMON's LineTypes\
    // (LinetypeCommands), CCL's window is NorsynApplicationsSHARED's Linetypes\Editor\,
    // both shared with NorsynDrawingToolsManaged, which registers CCL only: its window
    // does what the others do.
    public partial class Intersect
    {
        /// <command>CCL</command>
        /// <summary>
        /// Opens the complex linetype editor, a window that stays open beside the drawing:
        /// pick a linetype of the drawing (or the one of a selected object), edit its dashes,
        /// texts and shapes with a live preview, generate the text linetypes CCLS, CCLX and
        /// CCLXS made, make a pattern readable both ways, read and write .lin text, and write
        /// the linetype into the drawing (changed in place, so whatever uses it keeps it).
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CCL")]
        public void createcomplexlinetype() => LinetypeEditorWindowHost.Show();

        /// <command>CCLS</command>
        /// <summary>
        /// Creates complex linetype with text. User must type the name of the linetype and the text to be displayed.
        /// Uses single linetype dash for the text.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CCLS")]
        public void createcomplexlinetypesingle() => CreatePresetLinetype(LinetypePreset.TextBothWaysWhole);

        /// <command>CCLX</command>
        /// <summary>
        /// Creates complex linetype with text and an X. User must type the name of the linetype and the text to be displayed.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CCLX")]
        public void createcomplexlinetypex() => CreatePresetLinetype(LinetypePreset.TextWithX);

        /// <command>CCLXS</command>
        /// <summary>
        /// Creates complex linetype with text and an X. User must type the name of the linetype and the text to be displayed.
        /// Uses single linetype dash for the text.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CCLXS")]
        public void createcomplexlinetypexsingle() => CreatePresetLinetype(LinetypePreset.TextWithXWhole);

        /// <command>UELT</command>
        /// <summary>
        /// Update existing linetype. User must select object with linetype to be updated and type the text to be displayed.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("UELT")]
        public void updateexistinglinetype() =>
            LinetypeCommands.UpdateText(Application.DocumentManager.MdiActiveDocument, HostLinetypeFlags.Instance);

        /// <command>PRINTLT</command>
        /// <summary>
        /// Prints the definition of a linetype for debugging.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("PRINTLT")]
        public void printlinetypedefinition() =>
            LinetypeCommands.Print(Application.DocumentManager.MdiActiveDocument, HostLinetypeFlags.Instance);

        /// <command>UELTWS</command>
        /// <summary>
        /// Update Existing LineType With Symbol. Selects a polyline, extracts its linetype,
        /// and recreates it with upright rotation (U=0 and U=π) for better readability.
        /// Duplicates the original pattern - first copy with text at 0°, second copy with text at 180°.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("UELTWS")]
        public void updateexistinglinetypewithsymbol() =>
            LinetypeCommands.MakeReadableBothWays(Application.DocumentManager.MdiActiveDocument, HostLinetypeFlags.Instance);

        /// <command>UELTNS</command>
        /// <summary>
        /// Update Existing LineType No Symbol. Selects a polyline, extracts its linetype,
        /// and recreates it with upright rotation (U=0 and U=π) for better readability.
        /// Duplicates the original pattern - first copy with text at 0°, second copy with text at 180°.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("UELTNS")]
        public void updateexistinglinetypenosymbol() =>
            LinetypeCommands.MakeReadableBothWays(Application.DocumentManager.MdiActiveDocument, HostLinetypeFlags.Instance);

        /// <command>CREATEALLLINETYPESLAYERS</command>
        /// <summary>
        /// Creates a layer and a polyline for each linetype in drawing with the linetype assigned.
        /// Arranges the polylines in a table-like fashion.
        /// Useful for visualizing linetypes.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CREATEALLLINETYPESLAYERS")]
        public void createcalllinetypeslayers() =>
            LinetypeCommands.DrawSampleSheet(Application.DocumentManager.MdiActiveDocument.Database);

        /// <summary>Asks for a linetype name and a text, and writes the preset's linetype in Standard.</summary>
        private static void CreatePresetLinetype(LinetypePreset preset)
        {
            string? lineTypeName = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Line type name", "Enter LineType name: (ex: BIPS_TEXT_N2X) \n");
            if (string.IsNullOrEmpty(lineTypeName)) return;
            string? text = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Text", "Enter text to be displayed by line: (ex: 10kV)\n");
            if (string.IsNullOrEmpty(text)) return;
            LinetypeCommands.Create(
                Application.DocumentManager.MdiActiveDocument.Database,
                preset, lineTypeName, text, "Standard", HostLinetypeFlags.Instance);
        }
    }
}
