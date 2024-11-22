using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModel
{
    internal static class Tolerance
    {
        //public const double Default = 1e-6;
        public static readonly double Default = Autodesk.AutoCAD.Geometry.Tolerance.Global.EqualPoint;
    }
}
