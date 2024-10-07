using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class El04 : BlockBase
    {
        public El04() : base("EL 0.4kV") { }

        internal override void HandleBlockDefinition(Database localDb)
        {
            localDb.CheckOrImportBlockRecord(
                @"X:\AutoCAD DRI - 01 Civil 3D\Projection_styles.dwg", _blockName);
        }
    }
}
