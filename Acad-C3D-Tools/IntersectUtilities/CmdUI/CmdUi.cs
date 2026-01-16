using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.CmdUI.UI;

namespace IntersectUtilities
{
    /// <summary>
    /// Commands for the IntersectUtilities UI.
    /// </summary>
    public partial class Intersect
    {
        /// <command>IUCFG</command>
        /// <summary>
        /// Opens the CSV Configuration palette for selecting which configuration (V1, V2, etc.) to use.
        /// The selected configuration determines which versioned CSV files are loaded by commands.
        /// </summary>
        /// <category>INTERSECT UTILITIES</category>
        [CommandMethod("NSCMD")]
        public void OpenConfigurationPalette()
        {
            ConfigPaletteSet.Show();
        }
    }
}
