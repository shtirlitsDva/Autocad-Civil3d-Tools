using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class El10 : BlockBase
    {
        public El10()
            : base("EL 10kVv2") { }

        private double dia = 0.070;

        internal override void HandleBlockDefinition(Database localDb)
        {
            CreateBlockTableRecord(localDb, dia);
        }

        protected override double getDia() => dia;
    }
}
