using DevReload;

namespace NSPaletteSet
{
    public class NSPalettePlugin : IPlugin
    {
        public void Initialize() { }

        public object CreatePaletteSet()
        {
            return new MyPaletteSet();
        }

        public void Terminate() { }
    }
}
