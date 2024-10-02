using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal interface IProfileViewSymbol
    {
        void CreateSymbol(Transaction tx, BlockTableRecord detailingBlock, Point3d location, double dia, string layer);
    }
}
