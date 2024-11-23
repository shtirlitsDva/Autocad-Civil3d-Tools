using Autodesk.AutoCAD.Geometry;

using Dimensionering.DimensioneringV2.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2
{
    internal static class Extensions
    {
        public static Point2D To2D(this Point3d pt)
        {
            return new Point2D(pt.X, pt.Y);
        }
    }
}
