using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities;

namespace NTRExport.Utils
{
    internal static class Utils
    {
        public static string LTGMain(Handle source)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var ent = source.Go<Entity>(db);

            return PropertySetManager.ReadNonDefinedPropertySetString(
                ent, "DriPipelineData", "BelongsToAlignment");
        }

        public static string LTGBranch(Handle source)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var ent = source.Go<Entity>(db);

            return PropertySetManager.ReadNonDefinedPropertySetString(
                ent, "DriPipelineData", "BranchesOffToAlignment");
        }
    }
}
