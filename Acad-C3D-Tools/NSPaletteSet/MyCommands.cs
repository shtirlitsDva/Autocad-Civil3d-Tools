using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using static IntersectUtilities.UtilsCommon.Utils;

[assembly: CommandClass(typeof(NSPaletteSet.MyCommands))]

namespace NSPaletteSet
{
    public class MyCommands : IExtensionApplication
    {
        public void Initialize()
        {
            _myPs = null;
            prdDbg("NSPALETTE er klar! <-- BEMÆRK! Nyt navn.");
        }

        public void Terminate()
        {
            _myPs = null;
        }

        private static MyPaletteSet _myPs = null;

        /// <command>NSPALETTE</command>
        /// <summary>
        /// Åbner Norsyns palette med værktøjer til fjernvarme.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("NSPALETTE")]
        public static void ShowMyPaletteSet()
        {
            bool firstShow = false;
            if (_myPs==null)
            {
                _myPs = new MyPaletteSet();
                firstShow = true;
            }

            _myPs.Visible = true;

            // If you want the PaletteSet to be shown in certain way whenever it is be created the first time
            if (firstShow)
            {
                _myPs.Dock = Autodesk.AutoCAD.Windows.DockSides.Right;
                _myPs.Size = new System.Drawing.Size(500, 1500);
            }
        }        
    }
}
