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
    /// Enumerates all minimal subgraphs connecting the given terminals,
    /// but returns an empty list if the time limit is exceeded *before* it finishes.
    /// "Minimal" => removing any chosen edge disconnects at least one terminal.
    /// </summary>
    internal class MinimalSteinerEnumerator
    {
        private readonly UndirectedGraph<BFNode, BFEdge> _graph;
        private readonly HashSet<BFNode> _terminals;
        private readonly TimeSpan _timeLimit;

        // We store all solutions here in memory, only returning them if we finish in time.
        private readonly List<List<BFEdge>> _solutions;

        private bool _timeExpired;
        private DateTime _startTime;

        public MinimalSteinerEnumerator(UndirectedGraph<BFNode, BFEdge> graph,
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
        /// Public method: enumerates all minimal subgraphs. If time limit is exceeded
        /// before we finish, we return an empty list; otherwise, we return the full set.
        /// </summary>
        public List<List<BFEdge>> GetAllMinimalSubgraphs()
        {
            _startTime = DateTime.UtcNow;

            // If the full graph doesn't connect the terminals, no solutions anyway
            if (!IsConnected(_graph.Vertices, _graph.Edges))
                return new List<List<BFEdge>>();

            // Sort edges by ID for consistent ordering
            var allEdges = _graph.Edges.OrderBy(e => e.Id).ToList();

            // Start with all edges selected
            var chosenEdges = new List<BFEdge>(allEdges);

            // Recurse to find all minimal subgraphs
            Recurse(0, allEdges, chosenEdges);

            // If time expired, we discard the solutions and return empty
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
        /// Depth-first backtracking.
        /// For edge "allEdges[index]", we try removing it if removing it doesn't
        /// immediately disconnect terminals. We also consider keeping it. 
        /// At the end, if subgraph is connected, we do a minimality check.
        /// </summary>
        private void Recurse(int index, List<BFEdge> allEdges, List<BFEdge> chosenEdges)
        {
            // If time limit exceeded, just stop recursing
            if (TimeExceeded()) return;

            if (index >= allEdges.Count)
            {
                // Decided all edges, check connectivity and minimality
                if (IsConnected(_graph.Vertices, chosenEdges) && IsMinimal(chosenEdges))
                {
                    // Store solution in _solutions
                    _solutions.Add(new List<BFEdge>(chosenEdges));
                }
                return;
            }

            var edge = allEdges[index];

            // Try removing it (if it doesn't break connectivity among terminals)
            chosenEdges.Remove(edge);
            if (IsConnected(_graph.Vertices, chosenEdges))
            {
                Recurse(index + 1, allEdges, chosenEdges);
                if (TimeExceeded()) return;
            }

            // Backtrack: put it back
            chosenEdges.Add(edge);

            // Then also branch "keep the edge"
            Recurse(index + 1, allEdges, chosenEdges);
        }

        /// <summary>
        /// Return true if removing any edge from 'edges' breaks connectivity among terminals.
        /// If there's an edge we can remove and still remain connected, it's not minimal.
        /// </summary>
        private bool IsMinimal(List<BFEdge> edges)
        {
            // If 0 or 1 edges, it's trivially minimal if it connects the terminals
            if (edges.Count <= 1) return true;

            for (int i = 0; i < edges.Count; i++)
            {
                if (TimeExceeded()) return false; // if we run out of time, we skip further checks

                var removed = edges[i];
                edges.RemoveAt(i);

                bool stillConnected = IsConnected(_graph.Vertices, edges);

                // Put the edge back
                edges.Insert(i, removed);

                if (stillConnected)
                {
                    // We found an edge that can be removed w/o losing terminal connectivity => not minimal
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// BFS-based check to see if all terminals share a connected component in 'edges'.
        /// </summary>
        private bool IsConnected(IEnumerable<BFNode> vertices, IEnumerable<BFEdge> edges)
        {
            var sub = new UndirectedGraph<BFNode, BFEdge>(allowParallelEdges: false);
            sub.AddVertexRange(vertices);
            sub.AddEdgeRange(edges);

            if (_terminals.Count == 0) return true;

            var first = _terminals.First();
            var visited = new HashSet<BFNode> { first };
            var queue = new Queue<BFNode>();
            queue.Enqueue(first);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var e in sub.AdjacentEdges(current))
                {
                    var neighbor = (e.Source == current) ? e.Target : e.Source;
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Must visit all terminals
            return _terminals.All(visited.Contains);
        }

        /// <summary>
        /// Check if time limit is exceeded. If yes, sets the _timeExpired flag.
        /// </summary>
        private bool TimeExceeded()
        {
            if (_timeExpired) return true;
            if (DateTime.UtcNow - _startTime >= _timeLimit)
            {
                _timeExpired = true;
                return true;
            }
            return false;
        }
    }
}