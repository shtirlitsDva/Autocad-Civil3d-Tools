using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;

namespace DRILOAD
{
    public partial class DriLoad : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nUse DRILOAD to load DRI programs!");
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("DRILOAD")]
        public void DRILOAD()
        {

        }
    }
}

