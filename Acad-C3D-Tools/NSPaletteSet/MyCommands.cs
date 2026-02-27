using Autodesk.AutoCAD.Runtime;

using static IntersectUtilities.UtilsCommon.Utils;

[assembly: CommandClass(typeof(NSPaletteSet.MyCommands))]

namespace NSPaletteSet
{
    public class MyCommands : IExtensionApplication
    {
        private static MyPaletteSet? _myPs;

        public void Initialize()
        {
            _myPs = null;
            prdDbg("NSPALETTE er klar!");
        }

        public void Terminate()
        {
            _myPs = null;
        }

        /// <command>NSPALETTE</command>
        /// <summary>
        /// Åbner Norsyns palette med værktøjer til fjernvarme.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("NSPALETTE")]
        public static void ShowMyPaletteSet()
        {
            bool firstShow = false;
            if (_myPs == null)
            {
                _myPs = new MyPaletteSet();
                firstShow = true;
            }

            _myPs.Visible = true;

            if (firstShow)
            {
                _myPs.Dock = Autodesk.AutoCAD.Windows.DockSides.Right;
                _myPs.Size = new System.Drawing.Size(500, 1500);
            }
        }
    }
}
