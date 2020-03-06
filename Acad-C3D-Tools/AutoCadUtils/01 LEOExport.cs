using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace AutoCadUtils
{
    public class ExportLeoComponentsInRooms : IExtensionApplication
    {
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\n-> Export LEO components to EXCEL: EXPORTLEO");
        }

        public void Terminate()
        {
        }

        [CommandMethod("exportleo")]
        public void exportleo()
        {

        }
    }
}
