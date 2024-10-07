﻿using Autodesk.AutoCAD.ApplicationServices;
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
    internal class El10 : BlockBase
    {
        public El10() : base("EL 10kV") { }

        internal override void HandleBlockDefinition(Database localDb)
        {
            throw new NotImplementedException();
        }
    }
}
