using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModel
{
    internal class Point2DEqualityComparer : IEqualityComparer<Point2D>
    {
        private readonly double Tolerance = GraphModel.Tolerance.Default;

        public bool Equals(Point2D p1, Point2D p2)
        {
            return p1.Equals(p2);
        }

        public int GetHashCode(Point2D p)
        {
            return p.GetHashCode();
        }
    }
}
