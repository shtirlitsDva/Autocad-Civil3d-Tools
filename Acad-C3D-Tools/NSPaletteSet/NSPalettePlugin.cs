using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(NSPaletteSet.NSPalettePlugin))]
[assembly: CommandClass(typeof(NSPaletteSet.NoCommands))]

namespace NSPaletteSet
{
    public class NSPalettePlugin : IExtensionApplication
    {
        private static MyPaletteSet? _palette;

        public void Initialize() { }

        public void Terminate()
        {
            if (_palette != null)
            {
                _palette.Close();
                _palette.Dispose();
                _palette = null;
            }
        }

        [CommandMethod("NSPALETTE")]
        public static void ShowPalette()
        {
            if (_palette == null)
                _palette = new MyPaletteSet();
            _palette.Visible = true;
        }
    }

    public class NoCommands { }
}
