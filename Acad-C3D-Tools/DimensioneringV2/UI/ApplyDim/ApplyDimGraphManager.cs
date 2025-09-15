using DimensioneringV2.GraphFeatures;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.UI.ApplyDim
{
    internal class ApplyDimGraphManager
    {
        private readonly Dictionary<AnalysisFeature, UndirectedGraph<NodeJunction, EdgePipeSegment>> _featureToGraph = new();
        private readonly Dictionary<AnalysisFeature, EdgePipeSegment> _featureToEdge = new();

        public ApplyDimGraphManager(IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs)
        {
            Build(graphs);
        }

        private void Build(IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs)
        {
            _featureToGraph.Clear();
            _featureToEdge.Clear();
            foreach (var g in graphs)
            {
                foreach (var e in g.Edges)
                {
                    var feature = e.PipeSegment;
                    if (feature == null) continue;
                    _featureToGraph[feature] = g;
                    _featureToEdge[feature] = e;
                }
            }
        }

        public bool AreInSameGraph(AnalysisFeature a, AnalysisFeature b)
        {
            return _featureToGraph.TryGetValue(a, out var ga)
                && _featureToGraph.TryGetValue(b, out var gb)
                && ReferenceEquals(ga, gb);
        }

        public IEnumerable<AnalysisFeature>? TryGetShortestPath(AnalysisFeature start, AnalysisFeature target)
        {
            if (!_featureToGraph.TryGetValue(start, out var g)) return null;
            if (!_featureToGraph.TryGetValue(target, out var g2) || !ReferenceEquals(g, g2)) return null;
            if (!_featureToEdge.TryGetValue(start, out var eStart)) return null;
            if (!_featureToEdge.TryGetValue(target, out var eTarget)) return null;

            // Evaluate four endpoint combinations and pick the shortest
            var startNodes = new[] { eStart.Source, eStart.Target };
            var targetNodes = new[] { eTarget.Source, eTarget.Target };

            IEnumerable<EdgePipeSegment>? bestPath = null;
            double bestCost = double.PositiveInfinity;

            foreach (var sn in startNodes)
            {
                var tryGetPaths = g.ShortestPathsDijkstra(edge => edge.PipeSegment.Length, sn);
                foreach (var tn in targetNodes)
                {
                    if (tryGetPaths(tn, out var path))
                    {
                        double cost = 0;
                        foreach (var pe in path) cost += pe.PipeSegment.Length;
                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            bestPath = path;
                        }
                    }
                }
            }

            if (bestPath == null) return null;
            var list = bestPath.Select(pe => pe.PipeSegment).ToList();
            // Ensure start and target are included
            if (!list.Contains(start)) list.Insert(0, start);
            if (!list.Contains(target)) list.Add(target);
            return list;
        }
    }
}


