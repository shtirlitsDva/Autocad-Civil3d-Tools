using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial
{
    internal readonly struct OrientedRect
    {
        public Point2d Origin { get; } // at A
        public double Ux { get; }      // unit x-axis (along line)
        public double Uy { get; }
        public double Vx { get; }      // unit y-axis (left normal)
        public double Vy { get; }
        public double Length { get; }
        public double HalfWidth { get; }

        public OrientedRect(Point2d origin, double ux, double uy, double vx, double vy, double length, double halfWidth)
        {
            Origin = origin; Ux = ux; Uy = uy; Vx = vx; Vy = vy; Length = length; HalfWidth = halfWidth;
        }

        public Point2d Corner00 => new Point2d(Origin.X - HalfWidth * Vx, Origin.Y - HalfWidth * Vy); // A - w*v
        public Point2d Corner01 => new Point2d(Origin.X + HalfWidth * Vx, Origin.Y + HalfWidth * Vy); // A + w*v
        public Point2d Corner11 => new Point2d(Origin.X + Length * Ux + HalfWidth * Vx, Origin.Y + Length * Uy + HalfWidth * Vy); // B + w*v
        public Point2d Corner10 => new Point2d(Origin.X + Length * Ux - HalfWidth * Vx, Origin.Y + Length * Uy - HalfWidth * Vy); // B - w*v
    }
}
