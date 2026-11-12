using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(NSPaletteSet.NSPalettePlugin))]

namespace NSPaletteSet
{
    public class NSPalettePlugin : IExtensionApplication
    {
        private static MyPaletteSet? _palette;

        public void Initialize()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\n(づ￣ ³￣)づ NSPaletteSet loaded! ヾ(＾∇＾)\n");
        }

        public void Terminate()
        {
            if (_palette != null)
            {
                _palette.Close();
                _palette.Dispose();
                _palette = null;
            }
        }
        /// <command>NSPALETTE</command>
        /// <summary>
        /// Opens the NSPalette window.
        /// </summary>
        /// <category>Palettes</category>
        /// <execution>
        /// Run NSPALETTE after loading the NSPalette library. The palette is created on first use and brought into view on subsequent runs.
        /// </execution>
        [CommandMethod("NSPALETTE")]
        public static void ShowPalette()
        {
            if (_palette == null)
                _palette = new MyPaletteSet();
            _palette.Visible = true;
        }
    }
}
