using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

using Autodesk.AutoCAD.Geometry;

using NetTopologySuite.Geometries;

namespace DimensioneringV2.Geometry
{
    internal readonly struct Point2D
    {
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
                return CoordinateTolerance.ArePointsEqual(X, Y, other.X, other.Y);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return CoordinateTolerance.ComputeHashCode(X, Y);
        }
        [JsonIgnore]
        public Coordinate Coordinate => new Coordinate(X, Y);
        public Point2d To2d() => new Point2d(X, Y);
        public Point3d To3d() => new Point3d(X, Y, 0);
        // Implement == and != operators
        public static bool operator ==(Point2D p1, Point2D p2) => p1.Equals(p2);
        public static bool operator !=(Point2D p1, Point2D p2) => !p1.Equals(p2);
        public override string ToString()
        {
            return $"X: {X}\nY: {Y}";
        }
    }
}
