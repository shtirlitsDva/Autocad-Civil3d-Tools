using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class El50 : BlockBase
    {
        public El50() : base("EL 50kV") { }
        private double dia = 0.07;
        internal override void HandleBlockDefinition(Database localDb)
        {
            CreateBlockTableRecord(localDb, dia);
        }
        protected override double getDia() => dia;
    }
}