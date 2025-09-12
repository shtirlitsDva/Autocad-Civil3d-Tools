using DimensioneringV2.GraphFeatures;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.UI.ApplyDim
{
    /// <summary>
    /// Builds and manages per-graph line graphs for AngivDim. In the line graph each original
    /// edge (pipe segment) becomes a node, and there is an edge between two nodes if the
    /// corresponding original edges are adjacent (share a junction).
    /// </summary>
    internal class AngivGraphManager
    {
        internal class EdgeNode
        {
            public AnalysisFeature Feature { get; }
            public EdgeNode(AnalysisFeature feature) { Feature = feature; }
        }

        internal class EdgeAdj : IEdge<EdgeNode>
        {
            public EdgeNode Source { get; }
            public EdgeNode Target { get; }
            public EdgeAdj(EdgeNode source, EdgeNode target)
            {
                Source = source;
                Target = target;
            }
        }

        private readonly List<UndirectedGraph<EdgeNode, EdgeAdj>> _lineGraphs = new();
        private readonly Dictionary<AnalysisFeature, (UndirectedGraph<EdgeNode, EdgeAdj> g, EdgeNode n)> _featureToNode = new();

        public AngivGraphManager(IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs)
        {
            Build(graphs);
        }

        private void Build(IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs)
        {
            _lineGraphs.Clear();
            _featureToNode.Clear();

            foreach (var g in graphs)
            {
                var lg = new UndirectedGraph<EdgeNode, EdgeAdj>(allowParallelEdges: false);

                // Create node per original edge
                var edgeToNode = new Dictionary<EdgePipeSegment, EdgeNode>();
                foreach (var e in g.Edges)
                {
                    var node = new EdgeNode(e.PipeSegment);
                    edgeToNode[e] = node;
                    lg.AddVertex(node);
                    _featureToNode[e.PipeSegment] = (lg, node);
                }

                // Find root and build only parent↔child adjacencies to avoid sibling shortcuts
                var root = g.Vertices.FirstOrDefault(v => v.IsRootNode);
                if (root == null)
                {
                    // Fallback: pick lowest-degree vertex as root if not marked
                    root = g.Vertices.OrderBy(v => g.AdjacentDegree(v)).FirstOrDefault();
                    if (root == null)
                    {
                        _lineGraphs.Add(lg);
                        continue;
                    }
                }

                var visited = new HashSet<NodeJunction>();
                var parentEdge = new Dictionary<NodeJunction, EdgePipeSegment?>();

                var queue = new Queue<NodeJunction>();
                queue.Enqueue(root);
                visited.Add(root);
                parentEdge[root] = null;

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var incoming = parentEdge[current];

                    foreach (var e in g.AdjacentEdges(current))
                    {
                        // neighbor node across this edge
                        var neighbor = ReferenceEquals(e.Source, current) ? e.Target : e.Source;
                        if (!visited.Contains(neighbor))
                        {
                            // Connect incoming edge to this outgoing edge (parent↔child) if incoming exists
                            if (incoming != null)
                            {
                                var n1 = edgeToNode[incoming];
                                var n2 = edgeToNode[e];
                                lg.AddEdge(new EdgeAdj(n1, n2));
                            }

                            visited.Add(neighbor);
                            parentEdge[neighbor] = e;
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                _lineGraphs.Add(lg);
            }
        }

        public bool AreInSameGraph(AnalysisFeature a, AnalysisFeature b)
        {
            if (!_featureToNode.TryGetValue(a, out var ga)) return false;
            if (!_featureToNode.TryGetValue(b, out var gb)) return false;
            return ReferenceEquals(ga.g, gb.g);
        }

        public IEnumerable<AnalysisFeature>? TryGetShortestPath(AnalysisFeature start, AnalysisFeature target)
        {
            if (!_featureToNode.TryGetValue(start, out var gs)) return null;
            if (!_featureToNode.TryGetValue(target, out var gt)) return null;
            if (!ReferenceEquals(gs.g, gt.g)) return null;

            var g = gs.g;
            var source = gs.n;
            var dest = gt.n;

            var tryGetPaths = g.ShortestPathsDijkstra(
                e => e.Target.Feature.Length, // weight by feature length
                source);

            if (!tryGetPaths(dest, out var edgePath)) return null;

            // Reconstruct sequence of features including the start.
            var features = new List<AnalysisFeature> { source.Feature };
            foreach (var e in edgePath)
            {
                features.Add(e.Target.Feature);
            }
            return features;
        }
    }
}


