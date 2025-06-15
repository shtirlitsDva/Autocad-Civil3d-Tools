using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal static class Vector2dExtensions
    {
        internal static double CrossProduct(this Vector2d u, Vector2d v) => u.X * v.Y - u.Y * v.X;
    }
}
