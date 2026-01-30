using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Geometry
{
    /// <summary>
    /// Equality comparer for Point2D that uses the centralized CoordinateTolerance settings.
    /// Use this with Dictionary&lt;Point2D, T&gt; or HashSet&lt;Point2D&gt; for tolerance-aware lookups.
    /// </summary>
    internal class Point2DEqualityComparer : IEqualityComparer<Point2D>
    {
        public bool Equals(Point2D p1, Point2D p2)
        {
            // Delegates to Point2D.Equals which uses CoordinateTolerance
            return p1.Equals(p2);
        }

        public int GetHashCode(Point2D p)
        {
            // Delegates to Point2D.GetHashCode which uses CoordinateTolerance
            return p.GetHashCode();
        }
    }
}
