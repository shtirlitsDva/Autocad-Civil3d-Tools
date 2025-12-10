using DimensioneringV2.BruteForceOptimization;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphUtilities
{
    /// <summary>
    /// A solution: just the set of edge IDs that form a minimal Steiner Tree.
    /// </summary>
    internal class SteinerTreeSolution
    {
        public HashSet<int> EdgesUsed { get; }

        public SteinerTreeSolution(HashSet<int> edgesUsed)
        {
            EdgesUsed = edgesUsed;
        }
    }

    /// <summary>
    /// Standard Union-Find for connectivity checks.
    /// </summary>
    internal sealed class UnionFind
    {
        private readonly int[] _parent;
        private readonly int[] _rank;

        public UnionFind(int n)
        {
            _parent = new int[n];
            _rank = new int[n];
            for (int i = 0; i < n; i++)
            {
                _parent[i] = i;
                _rank[i] = 0;
            }
        }

        private UnionFind(int[] parent, int[] rank)
        {
            _parent = (int[])parent.Clone();
            _rank = (int[])rank.Clone();
        }

        public UnionFind Clone() => new UnionFind(_parent, _rank);

        public int Find(int x)
        {
            if (_parent[x] != x)
                _parent[x] = Find(_parent[x]);
            return _parent[x];
        }

        public void Union(int x, int y)
        {
            var rx = Find(x);
            var ry = Find(y);
            if (rx == ry) return;

            if (_rank[rx] < _rank[ry])
            {
                _parent[rx] = ry;
            }
            else if (_rank[rx] > _rank[ry])
            {
                _parent[ry] = rx;
            }
            else
            {
                _parent[ry] = rx;
                _rank[rx]++;
            }
        }

        public bool Connected(int x, int y) => Find(x) == Find(y);
    }

    /// <summary>
    /// BDD state: which edge index we're deciding, and the union-find for connectivity.
    /// </summary>
    internal sealed class BddState
    {
        public UnionFind UF { get; }
        public int EdgeIndex { get; }

        public BddState(UnionFind uf, int edgeIndex)
        {
            UF = uf;
            EdgeIndex = edgeIndex;
        }
    }

    /// <summary>
    /// Enumerator that finds all minimal Steiner Trees (no cost) within a time limit.
    /// If time is exceeded, it returns an empty solution list.
    /// </summary>
    internal class SteinerTreeEnumeratorV2
    {
        private readonly List<BFEdge> _edges;
        private readonly HashSet<int> _terminals;
        private readonly TimeSpan _timeLimit;

        private readonly List<SteinerTreeSolution> _solutions;
        private readonly HashSet<(int edgeIndex, string ufKey)> _visited;

        private bool _timeLimitReached;
        private DateTime _startTime;

        public SteinerTreeEnumeratorV2(UndirectedGraph<BFNode, BFEdge> graph,
                                     IEnumerable<BFNode> terminals,
                                     TimeSpan timeLimit)
        {
            // Assign node and edge ids
            foreach (var (node, index) in graph.Vertices.Select(
                    (node, index) => (node, index))) node.Id = index;

            foreach (var (edge, index) in graph.Edges.Select(
                    (edge, index) => (edge, index))) edge.Id = index;

            // Sort edges by their Id for consistent enumeration
            _edges = graph.Edges.OrderBy(e => e.Id).ToList();

            // We need a union-find sized for the largest node ID
            int maxNodeId = graph.Vertices.Max(v => v.Id);
            int nodeCount = maxNodeId + 1;

            // Convert BFNode -> int for terminals
            _terminals = terminals.Select(t => t.Id).ToHashSet();

            _timeLimit = timeLimit;
            _solutions = new List<SteinerTreeSolution>();
            _visited = new HashSet<(int, string)>();
            _timeLimitReached = false;
        }

        /// <summary>
        /// Perform the enumeration. If we exceed the time limit, an empty list is returned.
        /// </summary>
        public List<SteinerTreeSolution> EnumerateAll()
        {
            _startTime = DateTime.UtcNow;

            // Initialize union-find with enough capacity
            int capacity = Math.Max(_terminals.Max(), _edges.Any()
                ? _edges.Max(e => Math.Max(e.Source.Id, e.Target.Id))
                : 0) + 1;

            var uf = new UnionFind(capacity);
            var initial = new BddState(uf, 0);

            var chosenEdges = new HashSet<int>();
            DfsBdd(initial, chosenEdges);

            // If time limit reached, we discard results and return an empty list
            if (_timeLimitReached)
            {
                return new List<SteinerTreeSolution>();
            }
            else
            {
                return _solutions;
            }
        }

        public UndirectedGraph<BFNode, BFEdge> SolutionToGraph(
            SteinerTreeSolution solution)
        {
            var graph = new UndirectedGraph<BFNode, BFEdge>();
            var query = _edges.Where(x => solution.EdgesUsed.Contains(x.Id));
            foreach (var edge in query) graph.AddEdge(new BFEdge(edge));
            return graph;
        }

        /// <summary>
        /// The core DFS-based BDD. Recursively choose or skip each edge, 
        /// check connectivity, and do minimality checks.
        /// </summary>
        private void DfsBdd(BddState state, HashSet<int> chosenEdges)
        {
            // Check time limit before doing anything
            if ((DateTime.UtcNow - _startTime) >= _timeLimit)
            {
                _timeLimitReached = true;
                return;
            }

            // If we've used all edges, see if we connect all terminals
            if (state.EdgeIndex >= _edges.Count)
            {
                if (AllTerminalsConnected(state.UF))
                {
                    // Check minimality
                    if (IsMinimal(chosenEdges))
                    {
                        _solutions.Add(new SteinerTreeSolution(new HashSet<int>(chosenEdges)));
                    }
                }
                return;
            }

            // Build visited key
            var ufKey = GetUnionFindKey(state.UF);
            var key = (state.EdgeIndex, ufKey);
            if (_visited.Contains(key)) return;
            _visited.Add(key);

            // 1) Skip the edge
            DfsBdd(new BddState(state.UF.Clone(), state.EdgeIndex + 1), chosenEdges);
            if (_timeLimitReached) return;  // check again

            // 2) Take the edge
            var edge = _edges[state.EdgeIndex];
            var newUf = state.UF.Clone();
            newUf.Union(edge.Source.Id, edge.Target.Id);

            chosenEdges.Add(edge.Id);
            DfsBdd(new BddState(newUf, state.EdgeIndex + 1), chosenEdges);
            chosenEdges.Remove(edge.Id); // backtrack
        }

        private bool AllTerminalsConnected(UnionFind uf)
        {
            // If there's only 1 terminal, it's trivially connected
            if (_terminals.Count <= 1) return true;

            var first = _terminals.First();
            var rep = uf.Find(first);
            return _terminals.All(t => uf.Find(t) == rep);
        }

        /// <summary>
        /// Minimality check: removing any chosen edge should break connectivity.
        /// This is naive and can be expensive for large sets.
        /// </summary>
        private bool IsMinimal(HashSet<int> edgesUsed)
        {
            if (edgesUsed.Count <= 1) return true;

            foreach (var eId in edgesUsed)
            {
                // Check time during minimality checks as well
                if ((DateTime.UtcNow - _startTime) >= _timeLimit)
                {
                    _timeLimitReached = true;
                    return false;
                }

                // Build union-find without this edge
                int capacity = Math.Max(_terminals.Max(),
                    _edges.Any() ? _edges.Max(ed => Math.Max(ed.Source.Id, ed.Target.Id)) : 0) + 1;

                var uf = new UnionFind(capacity);
                foreach (var x in edgesUsed.Where(x => x != eId))
                {
                    var e = _edges.First(ed => ed.Id == x);
                    uf.Union(e.Source.Id, e.Target.Id);
                }

                if (AllTerminalsConnected(uf))
                {
                    // Not minimal if removing this edge still connects all terminals
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Create a string representing the union-find parents for visited-state caching.
        /// </summary>
        private string GetUnionFindKey(UnionFind uf)
        {
            var clone = uf.Clone();
            // We'll find the max node index to iterate
            int maxNode = Math.Max(_terminals.Max(),
                _edges.Any() ? _edges.Max(e => Math.Max(e.Source.Id, e.Target.Id)) : 0);

            var parents = new List<int>();
            for (int i = 0; i <= maxNode; i++)
            {
                parents.Add(clone.Find(i));
            }
            return string.Join("-", parents);
        }
    }
}