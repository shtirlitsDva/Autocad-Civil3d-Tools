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
    internal class ElLuft : BlockBase
    {
        public ElLuft() : base("EL LUFT") { }
        private double dia = 0.1;
        internal override void HandleBlockDefinition(Database localDb)
        {
            CreateBlockTableRecord(localDb, dia);
        }
        protected override double getDia() => dia;
        public override void CreateDistances(
            BlockTableRecord btr, Matrix3d transform, Point3d labelLocation,
            double dia, string layer, string distance, double kappeOd)
        {
            //Nothing to do
        }
    }
}