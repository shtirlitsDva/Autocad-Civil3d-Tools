using System.Windows.Interop;

using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.GraphWriteV2.Theming;
using IntersectUtilities.GraphWriteV2.Theming.UI;

using static IntersectUtilities.UtilsCommon.Utils;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>GRAPHWRITEV2THEME</command>
        /// <summary>
        /// Opens the Label Theme Designer: configure the node-label style, palette, and Series chips
        /// used by GRAPHWRITEV2. The chosen theme is saved and read on the next GRAPHWRITEV2 run.
        /// </summary>
        /// <category>Graph</category>
        [CommandMethod("GRAPHWRITEV2THEME")]
        public void graphwritev2theme()
        {
            try
            {
                var theme = LabelThemeStore.Load();
                var window = new LabelThemeDesignerWindow(theme);
                new WindowInteropHelper(window) { Owner = AcadApp.MainWindow.Handle };
                window.ShowDialog();

                if (window.Applied)
                    prdDbg($"GRAPHWRITEV2 label theme saved to:\n{LabelThemeStore.Path}");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
            }
        }
    }
}
