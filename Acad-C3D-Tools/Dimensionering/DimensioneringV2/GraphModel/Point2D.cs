using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

using Autodesk.AutoCAD.Geometry;

namespace Dimensionering.DimensioneringV2.GraphModel
{
    internal readonly struct Point2D
    {
        private static double Tolerance = GraphModel.Tolerance.Default;
        private static double ScaleFactor = 1e6;
        public double X { get; }
        public double Y { get; }
        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceSquaredTo(Point2D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return dx * dx + dy * dy;
        }

        public override bool Equals(object obj)
        {
            if (obj is Point2D other)
            {
                return Math.Abs(X - other.X) < Tolerance && Math.Abs(Y - other.Y) < Tolerance;
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // Quantize the values to the specified tolerance
                long xInt = (long)(X * ScaleFactor);
                long yInt = (long)(Y * ScaleFactor);

                // Combine the hash codes
                int hash = 17;
                hash = hash * 23 + xInt.GetHashCode();
                hash = hash * 23 + yInt.GetHashCode();
                return hash;
            }
        }

        public Point2d To2d() => new Point2d(X, Y);
        public Point3d To3d() => new Point3d(X, Y, 0);

        // Implement == and != operators
        public static bool operator ==(Point2D p1, Point2D p2) => p1.Equals(p2);
        public static bool operator !=(Point2D p1, Point2D p2) => !p1.Equals(p2);
    }
}
