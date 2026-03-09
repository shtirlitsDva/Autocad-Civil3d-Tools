using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.CmdUI.UI;

namespace IntersectUtilities
{
    /// <summary>
    /// Commands for the IntersectUtilities UI.
    /// </summary>
    public partial class Intersect
    {
        /// <command>NSCMD</command>
        /// <summary>
        /// Opens the NS Command Center palette set with Configuration and Batch Processing tabs.
        /// </summary>
        /// <category>INTERSECT UTILITIES</category>
        [CommandMethod("NSCMD")]
        public void OpenNsCmdPalette()
        {
            NsCmdPaletteSet.Show();
        }

        /// <command>BPUIV2</command>
        /// <summary>
        /// Opens the NS Command Center and activates the Batch Processing tab.
        /// </summary>
        /// <category>Batch Processing</category>
        [CommandMethod("BPUIV2")]
        public void OpenBatchProcessingPalette()
        {
            NsCmdPaletteSet.Show(1);
        }
    }
}
