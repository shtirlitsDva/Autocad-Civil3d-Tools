using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                SystemObjects.DynamicLinker.LoadModule(
                    "AcMPolygonObj" + Application.Version.Major + ".dbx", false, false);
            }
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
                new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
        }
        #endregion
    }
}
