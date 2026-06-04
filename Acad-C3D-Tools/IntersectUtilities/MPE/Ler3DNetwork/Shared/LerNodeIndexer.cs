using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.Ler3DNetwork
{
    // Stable integer id per distinct XY point within a tolerance, via a 3x3 grid
    // (cell = tolerance) + a real-distance check — the true-tolerance node identity
    // shared across Ler3DNetwork. GetOrAdd inserts and returns; Find returns an
    // existing id or -1.
    internal sealed class LerNodeIndexer
    {
        private readonly double _tol;
        private readonly double _tol2;
        private readonly Dictionary<(long, long), List<int>> _grid = new();
        private readonly List<Point2d> _points = new();

        public LerNodeIndexer(double tolerance)
        {
            _tol = tolerance;
            _tol2 = tolerance * tolerance;
        }

        public int Count => _points.Count;

        public int GetOrAdd(Point2d p)
        {
            int found = Find(p);
            if (found >= 0) return found;

            int id = _points.Count;
            _points.Add(p);
            (long, long) own = CellOf(p);
            if (!_grid.TryGetValue(own, out List<int>? list)) { list = new(); _grid[own] = list; }
            list.Add(id);
            return id;
        }

        public int Find(Point2d p)
        {
            (long cx, long cy) = CellOf(p);
            for (long dx = -1; dx <= 1; dx++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    if (!_grid.TryGetValue((cx + dx, cy + dy), out List<int>? bucket)) continue;
                    foreach (int id in bucket)
                    {
                        if (Sq(_points[id], p) <= _tol2) return id;
                    }
                }
            }
            return -1;
        }

        private (long, long) CellOf(Point2d p) =>
            ((long)Math.Floor(p.X / _tol), (long)Math.Floor(p.Y / _tol));

        private static double Sq(Point2d a, Point2d b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return (dx * dx) + (dy * dy);
        }
    }
}
