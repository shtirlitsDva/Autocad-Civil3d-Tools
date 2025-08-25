using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial
{
    internal sealed class SpatialGridCache
    {
        private readonly double _cellSize;
        private readonly Dictionary<(int, int), List<Segment2d>> _buckets;

        public SpatialGridCache(double cellSize)
        {
            _cellSize = cellSize;
            _buckets = new Dictionary<(int, int), List<Segment2d>>();
        }

        private (int, int) Key(double x, double y)
        {
            return ((int)Math.Floor(x / _cellSize), (int)Math.Floor(y / _cellSize));
        }

        public void Insert(Segment2d seg)
        {
            var min = seg.Bounds.MinPoint;
            var max = seg.Bounds.MaxPoint;
            var (x0, y0) = Key(min.X, min.Y);
            var (x1, y1) = Key(max.X, max.Y);

            for (int i = x0; i <= x1; i++)
            {
                for (int j = y0; j <= y1; j++)
                {
                    if (!_buckets.TryGetValue((i, j), out var list))
                    {
                        list = new List<Segment2d>();
                        _buckets[(i, j)] = list;
                    }
                    list.Add(seg);
                }
            }
        }

        public IEnumerable<Segment2d> Query(Extents2d queryBox)
        {
            var (x0, y0) = Key(queryBox.MinPoint.X, queryBox.MinPoint.Y);
            var (x1, y1) = Key(queryBox.MaxPoint.X, queryBox.MaxPoint.Y);

            var seen = new HashSet<Segment2d>();
            for (int i = x0; i <= x1; i++)
            {
                for (int j = y0; j <= y1; j++)
                {
                    if (_buckets.TryGetValue((i, j), out var list))
                    {
                        foreach (var seg in list)
                        {
                            if (seen.Add(seg))
                                yield return seg;
                        }
                    }
                }
            }
        }
    }
}
