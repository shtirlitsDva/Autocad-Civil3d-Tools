using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Services;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphUtilities
{
    internal class SteinerTreesEnumeratorV4
    {
        /// <summary>
        /// Enumerates all Steiner trees that contain the specified root and terminals.
        /// Stops early if the time limit is reached.
        /// </summary>
        /// <param name="graph">An undirected graph (e.g. UndirectedGraph&lt;TVertex,TEdge&gt;).</param>
        /// <param name="root">The designated root node (supply node).</param>
        /// <param name="terminals">A set of terminal vertices that must be included.</param>
        /// <param name="timeLimit">Maximum allowed time for enumeration.</param>
        /// <param name="onTreeFound">
        /// Action callback invoked each time we find a valid Steiner tree (receives the edges of that tree).
        /// </param>
        public static void Enumerate<TVertex, TEdge>(
        IUndirectedGraph<TVertex, TEdge> graph,
        TVertex root,
        HashSet<TVertex> terminals,
        TimeSpan timeLimit,
        Action<List<TEdge>> onTreeFound
    )
        where TEdge : IEdge<TVertex>
        {
            // Start a stopwatch for time-limit checks
            var timer = Stopwatch.StartNew();

            // We'll keep track of which nodes are in our partial tree
            var visited = new HashSet<TVertex>();
            visited.Add(root);

            // The partial set of edges currently in the tree
            var partialTreeEdges = new List<TEdge>();

            // Frontier = edges that connect the visited set to some unvisited node
            var frontier = new List<TEdge>();
            foreach (var e in graph.AdjacentEdges(root))
            {
                var otherEnd = (e.Source.Equals(root)) ? e.Target : e.Source;
                frontier.Add(e);
            }

            // Recur until we either exhaust possibilities or run out of time
            Backtrack();

            // ----- Local function that does the recursive DFS -----
            void Backtrack()
            {
                // Check time limit at every entry
                if (timer.Elapsed > timeLimit)
                    return;

                // If all terminals are included, we have a Steiner tree
                if (terminals.All(t => visited.Contains(t)))
                {
                    // Report the current tree (make a copy of partialTreeEdges)
                    onTreeFound(new List<TEdge>(partialTreeEdges));
                    // **Return immediately to avoid adding extra nodes** 
                    // (ensures minimality; if you want *every* superset, remove this return).
                    return;
                }

                // Try each edge in the frontier as a candidate to expand the partial tree
                // We'll do a for-loop over the frontier so we can add/remove from it safely.
                for (int i = 0; i < frontier.Count; i++)
                {
                    // Time check again in case enumerations are long
                    if (timer.Elapsed > timeLimit)
                        return;

                    var edge = frontier[i];
                    TVertex u = edge.Source;
                    TVertex v = edge.Target;

                    // We know exactly one end is visited, the other is unvisited
                    // (if both ends were visited or unvisited, it wouldn't be in the frontier).
                    TVertex newNode = visited.Contains(u) ? v : u;
                    TVertex oldNode = visited.Contains(u) ? u : v;

                    // Add 'newNode' to the visited set
                    visited.Add(newNode);
                    partialTreeEdges.Add(edge);

                    // Add all edges from 'newNode' that go to unvisited nodes => new frontier edges
                    var newlyAddedFrontierEdges = new List<TEdge>();
                    foreach (var e2 in graph.AdjacentEdges(newNode))
                    {
                        var o2 = (e2.Source.Equals(newNode)) ? e2.Target : e2.Source;
                        if (!visited.Contains(o2))
                        {
                            newlyAddedFrontierEdges.Add(e2);
                        }
                    }
                    // Insert them at the end of the frontier
                    frontier.AddRange(newlyAddedFrontierEdges);

                    // Now we remove 'edge' from the frontier so we don't expand it again
                    // in the same partial tree. We'll put it back after recursion to backtrack properly.
                    // Because we've effectively consumed 'edge' in this path.
                    var removedEdge = frontier[i];
                    frontier.RemoveAt(i);
                    i--; // Adjust index so that the loop continues correctly

                    // Recurse
                    Backtrack();

                    // ----- Backtrack (undo everything) -----
                    // 1) Put 'edge' back into frontier at position i
                    frontier.Insert(i + 1, removedEdge);
                    i++;

                    // 2) Remove newly added edges
                    for (int r = 0; r < newlyAddedFrontierEdges.Count; r++)
                    {
                        frontier.RemoveAt(frontier.Count - 1);
                    }

                    // 3) Remove 'edge' from partial tree
                    partialTreeEdges.RemoveAt(partialTreeEdges.Count - 1);

                    // 4) Remove newNode from visited
                    visited.Remove(newNode);

                    // If time is up, no need to continue
                    if (timer.Elapsed > timeLimit)
                        return;
                }
            }
        }
    }
}