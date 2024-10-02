using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal abstract class ProfileViewSymbol : IProfileViewSymbol
    {
        public abstract void CreateSymbol(
            Transaction tx, BlockTableRecord detailingBlock, Point3d location, double dia, string layer);
    }
}
