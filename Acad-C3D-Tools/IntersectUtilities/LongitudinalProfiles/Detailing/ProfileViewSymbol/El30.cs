using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class El30 : BlockBase
    {
        public El30() : base("EL 30KVv2") { }

        internal override void HandleBlockDefinition(Database localDb)
        {
            CreateBlockTableRecord(localDb, 0.1);
        }
    }
}
