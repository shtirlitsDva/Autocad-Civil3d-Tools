using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Services;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphUtilities
{
    /// <summary>
    /// Enumerates "minimal" terminal-connected subgraphs by 
    /// only removing edges that are currently non-bridge edges.
    /// If time-limit is exceeded, returns empty. Otherwise returns all solutions.
    /// 
    /// "Minimal" here means: you cannot remove any more edges 
    /// (since any removable edge is non-bridge, and we keep removing them).
    /// Once no non-bridge edges remain, we have a subgraph 
    /// in which every edge is bridging the connectivity of the subgraph.
    /// </summary>
    internal class SteinerTreesEnumeratorV3
    {
        private readonly UndirectedGraph<BFNode, BFEdge> _graph;
        private readonly HashSet<BFNode> _terminals;
        private readonly TimeSpan _timeLimit;
        private readonly List<List<BFEdge>> _solutions;

        private DateTime _startTime;
        private bool _timeExpired;

        public SteinerTreesEnumeratorV3(UndirectedGraph<BFNode, BFEdge> graph,
                                   HashSet<BFNode> terminals,
                                   TimeSpan timeLimit)
        {
            _graph = graph;
            _terminals = terminals;
            _timeLimit = timeLimit;
            _timeExpired = false;

            _solutions = new List<List<BFEdge>>();
        }

        /// <summary>
        /// Returns all minimal subgraphs in which no more edges can be removed 
        /// (because all edges are bridging or needed to keep terminals connected).
        /// If the time limit is reached mid-enumeration, returns an empty list.
        /// </summary>
        public List<List<BFEdge>> Enumerate()
        {
            _startTime = DateTime.UtcNow;

            // Check if the full graph connects the terminals at all.
            if (!TerminalsConnected(_graph.Vertices, _graph.Edges))
                return new List<List<BFEdge>>();

            // Start recursion with all edges
            var allEdges = _graph.Edges.ToList();
            Recurse(allEdges);

            // If time expired, discard solutions
            if (_timeExpired)
            {
                return new List<List<BFEdge>>();
            }
            else
            {
                return _solutions;
            }
        }

        /// <summary>
        /// The core recursion. We:
        /// 1) Check if terminals remain connected
        /// 2) Find all "non-bridge" edges
        /// 3) For each non-bridge edge, remove it and recurse
        /// 4) If no non-bridge edges remain, store this subgraph as minimal
        /// </summary>
        private void Recurse(List<BFEdge> currentEdges)
        {
            // Check time limit
            if (TimeExceeded()) return;

            // Check if subgraph is still connecting the terminals
            if (!TerminalsConnected(_graph.Vertices, currentEdges)) return;

            // Now find which edges are bridges in the current subgraph
            var sub = BuildSubgraph(currentEdges);
            // Placeholder: you have your own bridging logic. 
            // We'll call a dummy method here:
            var bridges = FindBridges.DoFindThem(sub);

            // "non-bridges" = currentEdges minus the bridging edges
            var nonBridgeEdges = currentEdges.Except(bridges).ToList();

            if (nonBridgeEdges.Count == 0)
            {
                // No removable edges remain => we are minimal
                // Store this subgraph in the solutions
                _solutions.Add(new List<BFEdge>(currentEdges));
                return;
            }

            // Otherwise, for each non-bridge edge, remove it and recurse
            foreach (var edge in nonBridgeEdges)
            {
                if (TimeExceeded()) return;

                currentEdges.Remove(edge);
                // Recurse
                Recurse(currentEdges);
                // Backtrack
                currentEdges.Add(edge);

                if (_timeExpired) return;
            }
        }

        /// <summary>
        /// Build a subgraph for BFS or bridging checks.
        /// </summary>
        private UndirectedGraph<BFNode, BFEdge> BuildSubgraph(List<BFEdge> edges)
        {
            var sg = new UndirectedGraph<BFNode, BFEdge>();
            foreach (var v in _graph.Vertices) sg.AddVertex(v);
            sg.AddEdgeRange(edges);
            return sg;
        }

        /// <summary>
        /// Checks if all terminals are in one connected component 
        /// using BFS on 'edges'.
        /// </summary>
        private bool TerminalsConnected(IEnumerable<BFNode> vertices, IEnumerable<BFEdge> edges)
        {
            if (_terminals.Count == 0) return true;

            var g = new UndirectedGraph<BFNode, BFEdge>();
            g.AddVertexRange(vertices);
            g.AddEdgeRange(edges);

            var first = _terminals.First();
            var visited = new HashSet<BFNode> { first };
            var queue = new Queue<BFNode>();
            queue.Enqueue(first);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var e in g.AdjacentEdges(cur))
                {
                    var neighbor = (e.Source == cur) ? e.Target : e.Source;
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return _terminals.All(t => visited.Contains(t));
        }

        private bool TimeExceeded()
        {
            if (_timeExpired) return true;
            var elapsed = DateTime.UtcNow - _startTime;
            if (elapsed >= _timeLimit)
            {
                _timeExpired = true;
                return true;
            }
            return false;
        }
    }
}