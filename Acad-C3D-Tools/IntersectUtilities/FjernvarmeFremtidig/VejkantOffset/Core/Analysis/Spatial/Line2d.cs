using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial
{
    internal readonly struct Line2d
    {
        public Point2d A { get; }
        public Point2d B { get; }
        public Extents2d Bounds { get; }


        public Line2d(Point2d a, Point2d b)
        {
            A = a; B = b;
            Bounds = new Extents2d(
            new Point2d(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)),
            new Point2d(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y))
            );
        }
    }
}
