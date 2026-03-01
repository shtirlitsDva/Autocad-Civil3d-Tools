using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(NSPaletteSet.NoCommands))]

namespace NSPaletteSet
{
    public class NSPalettePlugin : IExtensionApplication
    {
        public void Initialize() { }
        public void Terminate() { }
    }

    public class NoCommands { }
}
