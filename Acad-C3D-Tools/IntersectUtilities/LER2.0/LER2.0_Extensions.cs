using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LER2
{
    public static class LER2_Extensions
    {
        public static bool IsOn(this Point3d p, MyPl3d pl) => pl.IsPointOn(p);
        public static bool IsOn(this Vertex3d p, MyPl3d pl) => pl.IsPointOn(p.Position);
    }
}
