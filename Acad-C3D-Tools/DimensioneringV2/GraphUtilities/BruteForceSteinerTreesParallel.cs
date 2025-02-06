using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Services;

using QuikGraph;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphUtilities
{
    internal class SteinerTreesEnumeratorV3Parallel
    {
        private readonly UndirectedGraph<BFNode, BFEdge> _graph;
        private readonly HashSet<BFNode> _terminals;
        private readonly TimeSpan _timeLimit;
        private DateTime _startTime;
        private volatile bool _timeExpired;

        // We'll store solutions in a thread-safe bag (or a lock-based structure).
        // If we finish in time, we convert to a List. If not, we discard them.
        private readonly ConcurrentBag<List<BFEdge>> _solutions;

        public SteinerTreesEnumeratorV3Parallel(UndirectedGraph<BFNode, BFEdge> graph,
                                           HashSet<BFNode> terminals,
                                           TimeSpan timeLimit)
        {
            _graph = graph;
            _terminals = terminals;
            _timeLimit = timeLimit;
            _timeExpired = false;
            _solutions = new ConcurrentBag<List<BFEdge>>();
        }

        public List<List<BFEdge>> Enumerate()
        {
            _startTime = DateTime.UtcNow;

            // Check if full graph connects terminals
            if (!TerminalsConnected(_graph.Vertices, _graph.Edges))
                return new List<List<BFEdge>>();

            // We'll use a queue to store "states" => each state is a subgraph's edge list
            var workQueue = new ConcurrentQueue<List<BFEdge>>();

            // Start with one state: the full set of edges
            workQueue.Enqueue(_graph.Edges.ToList());

            // We'll create multiple tasks that process the queue
            int maxDegreeOfParallelism = Environment.ProcessorCount;
            var tasks = new List<Task>();

            for (int i = 0; i < maxDegreeOfParallelism; i++)
            {
                tasks.Add(Task.Run(() => Worker(workQueue)));
            }

            // Wait for tasks to complete
            Task.WaitAll(tasks.ToArray());

            // If time expired, discard solutions
            if (_timeExpired)
                return new List<List<BFEdge>>();

            // Otherwise, return distinct solutions
            // (Optional) remove duplicates by signature
            var distinct = new HashSet<string>();
            var final = new List<List<BFEdge>>();

            foreach (var sol in _solutions)
            {
                var signature = string.Join(",", sol.Select(e => e.Id).OrderBy(x => x));
                if (!distinct.Contains(signature))
                {
                    distinct.Add(signature);
                    final.Add(sol);
                }
            }
            return final;
        }

        private void Worker(ConcurrentQueue<List<BFEdge>> queue)
        {
            while (!_timeExpired && queue.TryDequeue(out var currentEdges))
            {
                if (!TerminalsConnected(_graph.Vertices, currentEdges))
                    continue; // skip if not connected

                // find bridging edges in current subgraph
                var sub = BuildSubgraph(currentEdges);
                var bridges = FindBridges(sub, currentEdges);

                var nonBridges = currentEdges.Except(bridges).ToList();
                if (nonBridges.Count == 0)
                {
                    // minimal
                    _solutions.Add(new List<BFEdge>(currentEdges));
                }
                else
                {
                    // for each non-bridge, remove it => new sub-problem
                    foreach (var edgeToRemove in nonBridges)
                    {
                        if (TimeExceeded()) break;

                        var newEdges = new List<BFEdge>(currentEdges);
                        newEdges.Remove(edgeToRemove);

                        // push new state
                        queue.Enqueue(newEdges);
                    }
                }

                if (TimeExceeded()) break;
            }
        }

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

        private bool TerminalsConnected(IEnumerable<BFNode> vertices, IEnumerable<BFEdge> edges)
        {
            if (_terminals.Count == 0) return true;

            var g = new UndirectedGraph<BFNode, BFEdge>(allowParallelEdges: false);
            g.AddVertexRange(vertices);
            g.AddEdgeRange(edges);

            var first = _terminals.First();
            var visited = new HashSet<BFNode> { first };
            var queue = new Queue<BFNode>();
            queue.Enqueue(first);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var e in g.AdjacentEdges(current))
                {
                    var neighbor = (e.Source == current) ? e.Target : e.Source;
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return _terminals.All(t => visited.Contains(t));
        }

        private UndirectedGraph<BFNode, BFEdge> BuildSubgraph(List<BFEdge> edges)
        {
            var sub = new UndirectedGraph<BFNode, BFEdge>(allowParallelEdges: false);
            foreach (var v in _graph.Vertices)
                sub.AddVertex(v);
            sub.AddEdgeRange(edges);
            return sub;
        }

        /// <summary>
        /// Replace with your actual bridging logic. 
        /// This must return all edges in 'subgraph' that are 
        /// "bridges" with respect to the subgraph connectivity.
        /// </summary>
        private HashSet<BFEdge> FindBridges(UndirectedGraph<BFNode, BFEdge> subgraph,
                                            List<BFEdge> currentEdges)
        {
            // placeholder => everything is non-bridge
            return new HashSet<BFEdge>();
        }
    }
}