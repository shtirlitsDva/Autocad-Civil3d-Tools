using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>Identity of a consecutive pair: the shared vertex.</summary>
    internal readonly record struct VertexKey(long X, long Y)
    {
        private const int scale = 10_000;
        public static VertexKey From(Point2d p) => new VertexKey(
            (long)Math.Round(p.X * scale),
            (long)Math.Round(p.Y * scale));
    }
}
