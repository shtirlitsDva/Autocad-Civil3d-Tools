using QuikGraph;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphUtilities
{
    internal static class SteinerTreeEnumerator
    {
        /// <summary>
        /// Enumerates *all* subgraphs that contain the given terminals in a connected component,
        /// but aborts if the enumeration takes longer than 5 seconds. 
        /// Returns a List of subgraphs, each subgraph is a List of TEdge.
        /// </summary>
        public static List<List<TEdge>> EnumerateSteinerTrees<TVertex, TEdge>(
            UndirectedGraph<TVertex, TEdge> graph,
            IEnumerable<TVertex> terminals,
            TimeSpan timeLimit
        )
            where TEdge : IEdge<TVertex>
        {
            var _allSteinerTrees = new List<List<TEdge>>();

            var edgeList = graph.Edges.ToList();
            int edgeCount = edgeList.Count;

            // Convert the terminals to a HashSet for quick "contains" checks.
            var terminalSet = new HashSet<TVertex>(terminals);

            // Start the stopwatch with a 5-second limit
            Stopwatch sw = Stopwatch.StartNew();

            // We'll do a naive backtracking: pick or skip each edge in edgeList
            var currentEdges = new List<TEdge>();
            EnumerateRecursive(graph, edgeList, terminalSet, 0, currentEdges, sw, timeLimit, _allSteinerTrees);

            return _allSteinerTrees;
        }

        /// <summary>
        /// Recursive backtracking to include or skip each edge. If time > timeLimit, we abort.
        /// </summary>
        private static void EnumerateRecursive<TVertex, TEdge>(
            UndirectedGraph<TVertex, TEdge> graph,
            List<TEdge> edgeList,
            HashSet<TVertex> terminalSet,
            int index,
            List<TEdge> currentEdges,
            Stopwatch sw,
            TimeSpan timeLimit,
            List<List<TEdge>> _allSteinerTrees
        )
            where TEdge : IEdge<TVertex>
        {
            // If we've hit the time limit, stop exploring further.
            if (sw.Elapsed > timeLimit) { _allSteinerTrees.Clear(); return; }

            // If we've considered all edges:
            if (index == edgeList.Count)
            {
                // Check if 'currentEdges' connects all terminals
                if (ContainsAllTerminals(graph, terminalSet, currentEdges))
                {
                    // Store a copy so we don't mutate later
                    _allSteinerTrees.Add(new List<TEdge>(currentEdges));
                }
                return;
            }

            // 1) Skip this edge
            EnumerateRecursive(graph, edgeList, terminalSet, index + 1, currentEdges, sw, timeLimit, _allSteinerTrees);
            if (sw.Elapsed > timeLimit) { _allSteinerTrees.Clear(); return; }

            // 2) Include this edge
            currentEdges.Add(edgeList[index]);
            EnumerateRecursive(graph, edgeList, terminalSet, index + 1, currentEdges, sw, timeLimit, _allSteinerTrees);
            // Remove edge after returning
            currentEdges.RemoveAt(currentEdges.Count - 1);
        }

        /// <summary>
        /// Checks if the subgraph formed by 'selectedEdges' is connected and 
        /// contains all the terminals in one connected component.
        /// </summary>
        private static bool ContainsAllTerminals<TVertex, TEdge>(
            UndirectedGraph<TVertex, TEdge> graph,
            HashSet<TVertex> terminalSet,
            List<TEdge> selectedEdges
        )
            where TEdge : IEdge<TVertex>
        {
            if (terminalSet.Count == 0) return true; // trivially satisfied if no terminals

            // Build a subgraph of only 'selectedEdges'
            // We can do that by building a new UndirectedGraph
            var subGraph = new UndirectedGraph<TVertex, TEdge>(false);
            // First add all relevant vertices (only those that appear in selectedEdges, or all?)
            // We only need to ensure the subgraph has all the terminal vertices and those used by edges
            var allVerticesInSubgraph = new HashSet<TVertex>();
            foreach (var e in selectedEdges)
            {
                allVerticesInSubgraph.Add(e.Source);
                allVerticesInSubgraph.Add(e.Target);
            }
            foreach (var v in terminalSet)
                allVerticesInSubgraph.Add(v);

            subGraph.AddVertexRange(allVerticesInSubgraph);
            // Add edges
            foreach (var e in selectedEdges)
            {
                subGraph.AddEdge(e);
            }

            // Now check if all terminals are in the same connected component
            // We'll pick any terminal as a root and do BFS/DFS to see if we reach others.
            var firstTerm = default(TVertex);
            foreach (var t in terminalSet)
            {
                firstTerm = t;
                break;
            }

            // If for some reason there's no terminal, we can say "true" or decide differently
            if (firstTerm == null) return true;

            // BFS/DFS from 'firstTerm'
            var visited = new HashSet<TVertex>();
            var stack = new Stack<TVertex>();
            stack.Push(firstTerm);
            visited.Add(firstTerm);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                // For each neighbor in subGraph
                foreach (var edge in subGraph.AdjacentEdges(current))
                {
                    var neighbor = (edge.Source.Equals(current) ? edge.Target : edge.Source);
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        stack.Push(neighbor);
                    }
                }
            }

            // Finally, verify if all terminals are visited
            foreach (var term in terminalSet)
                if (!visited.Contains(term)) return false;

            return true;
        }
    }
}
