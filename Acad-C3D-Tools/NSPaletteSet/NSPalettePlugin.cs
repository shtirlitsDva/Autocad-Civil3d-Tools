using DevReload;

namespace NSPaletteSet
{
    public class NSPalettePlugin : IPlugin, IPluginPalette
    {
        public object CreatePaletteSet()
        {
            return new MyPaletteSet();
        }

        public void Terminate() { }
    }
}
