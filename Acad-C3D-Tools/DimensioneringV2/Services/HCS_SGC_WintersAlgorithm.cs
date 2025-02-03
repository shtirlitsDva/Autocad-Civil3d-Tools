using DimensioneringV2.BruteForceOptimization;
using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.SubGraphs
{
    internal static class HCS_SGC_WintersAlgorithm
    {
        /// <summary>
        /// Enumerate all spanning trees of the given undirected graph using Winter's algorithm.
        /// WARNING: Exponential growth for non-trivial graphs; use only for smaller or sparser graphs.
        /// </summary>
        /// <param name="originalGraph">The original QuikGraph undirected graph.</param>
        /// <returns>An enumerable of spanning trees, each represented as a list of BFEdge.</returns>
        public static IEnumerable<List<BFEdge>> EnumerateAllSpanningTrees(UndirectedGraph<BFNode, BFEdge> originalGraph)
        {
            if (originalGraph.VertexCount == 0)
                yield break;

            // 1. Build an internal 'ContractGraph' structure from the original QuikGraph
            var contractGraph = new ContractGraph(originalGraph);

            // 2. Recursively enumerate using Winter's algorithm
            // We carry along a 'chosenEdges' list that accumulates edges used in contractions.
            foreach (var spanningTree in EnumerateRecursive(contractGraph, new List<BFEdge>()))
            {
                yield return spanningTree;
            }
        }

        /// <summary>
        /// Recursive function that implements Winter's contraction logic.
        /// </summary>
        private static IEnumerable<List<BFEdge>> EnumerateRecursive(ContractGraph g, List<BFEdge> chosenEdges)
        {
            // If the graph is down to 1 active vertex, we have a spanning tree
            if (g.ActiveVertices.Count == 1)
            {
                // The edges in 'chosenEdges' form a spanning tree in the original graph
                yield return new List<BFEdge>(chosenEdges);
                yield break;
            }

            // 1. Identify the highest-labeled vertex
            int n_i = g.ActiveVertices.Max(); // highest label

            // 2. Gather neighbors of n_i
            var neighbors = g.GetNeighbors(n_i).ToList(); // distinct neighbor labels

            // If no neighbors, we cannot form a spanning tree from here
            if (neighbors.Count == 0)
                yield break;

            // For each neighbor n_j, branch into:
            //   (A) Contract n_i -> n_j
            //   (B) Delete edges between n_i and n_j (only if n_i has other neighbors)
            foreach (int n_j in neighbors)
            {
                // All parallel edges between n_i and n_j
                var edgeSet = g.GetEdgesBetween(n_i, n_j);

                // --- (A) CONTRACT ROUTE ---
                {
                    // Create a copy of 'g' for the contracted route
                    var gContracted = g.Clone();

                    // Also clone the chosenEdges
                    var chosenEdgesContracted = new List<BFEdge>(chosenEdges);
                    // Add the edges that we are contracting
                    chosenEdgesContracted.AddRange(edgeSet);

                    // Perform the contraction n_i -> n_j
                    gContracted.Contract(n_i, n_j);

                    // Recurse
                    foreach (var st in EnumerateRecursive(gContracted, chosenEdgesContracted))
                        yield return st;
                }

                // --- (B) DELETE ROUTE ---
                // "Deletion of S(n_i, n_j) occurs only when n_i has adjacent vertices
                // other than n_j" - (i.e., there's more than 1 neighbor)
                if (neighbors.Count > 1)
                {
                    var gDeleted = g.Clone();
                    var chosenEdgesDeleted = new List<BFEdge>(chosenEdges);

                    // Remove all edges between n_i and n_j
                    gDeleted.DeleteEdges(n_i, n_j);

                    // Recurse on the new graph (still have n_i and n_j, but no edges between them)
                    foreach (var st in EnumerateRecursive(gDeleted, chosenEdgesDeleted))
                        yield return st;
                }
            }
        }

        /// <summary>
        /// Internal representation for the "contractible" graph.
        /// Adjacency is stored as an int -> Dictionary(int -> List(BFEdge)) to handle parallel edges easily.
        /// We keep a set of "ActiveVertices" that remain uncontracted.
        /// </summary>
        private class ContractGraph
        {
            // Maps label -> the BFNode(s) that label represents. In simple usage, 
            // you can just store one BFNode per label if you prefer, or keep sets if needed.
            // Here we assume 1-to-1 for labeling from the original indexing, for demonstration.
            private Dictionary<int, BFNode> labelToNode = new Dictionary<int, BFNode>();

            // Adjacency: adjacency[u][v] = list of edges connecting label u and label v
            private Dictionary<int, Dictionary<int, List<BFEdge>>> adjacency
                = new Dictionary<int, Dictionary<int, List<BFEdge>>>();

            public HashSet<int> ActiveVertices { get; private set; } = new HashSet<int>();

            public int VertexCount => ActiveVertices.Count;

            /// <summary>
            /// Construct from an original QuikGraph.
            /// Assign labels 0..(n-1) to BFNode in ascending ID or some stable ordering.
            /// </summary>
            public ContractGraph(UndirectedGraph<BFNode, BFEdge> original)
            {
                // 1. Sort the nodes by their BFNode.Id or any stable property
                //    Then label them from 0..(n-1)
                var allNodesSorted = original.Vertices.OrderBy(n => n.Id).ToList();

                for (int label = 0; label < allNodesSorted.Count; label++)
                {
                    labelToNode[label] = allNodesSorted[label];
                    ActiveVertices.Add(label);
                }

                // 2. Build adjacency
                foreach (var label in ActiveVertices)
                {
                    adjacency[label] = new Dictionary<int, List<BFEdge>>();
                }

                // For each edge in the original graph, find the labels of Source and Target
                foreach (var edge in original.Edges)
                {
                    int labelA = allNodesSorted.IndexOf(edge.Source);
                    int labelB = allNodesSorted.IndexOf(edge.Target);

                    if (labelA == labelB)
                        continue; // ignore self-loops if any

                    // Ensure adjacency is symmetrical
                    if (!adjacency[labelA].ContainsKey(labelB))
                        adjacency[labelA][labelB] = new List<BFEdge>();
                    if (!adjacency[labelB].ContainsKey(labelA])
                        adjacency[labelB][labelA] = new List<BFEdge>();

                    adjacency[labelA][labelB].Add(edge);
                    adjacency[labelB][labelA].Add(edge);
                }
            }

            private ContractGraph() { } // private blank for Clone usage

            /// <summary>
            /// Deep clone the ContractGraph so we can do destructive modifications (contract/delete) safely.
            /// </summary>
            public ContractGraph Clone()
            {
                var copy = new ContractGraph();
                copy.labelToNode = new Dictionary<int, BFNode>(this.labelToNode);
                copy.ActiveVertices = new HashSet<int>(this.ActiveVertices);

                // Deep copy adjacency
                copy.adjacency = new Dictionary<int, Dictionary<int, List<BFEdge>>>();
                foreach (var kv in this.adjacency)
                {
                    int u = kv.Key;
                    copy.adjacency[u] = new Dictionary<int, List<BFEdge>>();
                    foreach (var kv2 in kv.Value)
                    {
                        int v = kv2.Key;
                        // Copy the BFEdge list
                        copy.adjacency[u][v] = new List<BFEdge>(kv2.Value);
                    }
                }

                return copy;
            }

            /// <summary>
            /// Return all neighbor labels of the given label n_i.
            /// </summary>
            public IEnumerable<int> GetNeighbors(int n_i)
            {
                if (!adjacency.ContainsKey(n_i))
                    yield break;

                foreach (var kv in adjacency[n_i])
                {
                    if (ActiveVertices.Contains(kv.Key))
                        yield return kv.Key;
                }
            }

            /// <summary>
            /// Return the list of edges (possibly parallel) between n_i and n_j.
            /// </summary>
            public List<BFEdge> GetEdgesBetween(int n_i, int n_j)
            {
                if (adjacency.ContainsKey(n_i) &&
                    adjacency[n_i].ContainsKey(n_j))
                {
                    return adjacency[n_i][n_j];
                }
                return new List<BFEdge>();
            }

            /// <summary>
            /// Contract vertex n_i into n_j: 
            /// Merge adjacency of n_i into n_j, remove n_i from ActiveVertices.
            /// </summary>
            public void Contract(int n_i, int n_j)
            {
                if (n_i == n_j) return;

                // For each neighbor 'x' of n_i, move adjacency to n_j
                if (adjacency.TryGetValue(n_i, out var neighborsOfI))
                {
                    foreach (var (x, edgesIX) in neighborsOfI)
                    {
                        if (x == n_j) continue; // We'll handle that in a moment

                        if (!adjacency[n_j].ContainsKey(x))
                            adjacency[n_j][x] = new List<BFEdge>();

                        // Add edges from n_i->x to n_j->x
                        adjacency[n_j][x].AddRange(edgesIX);

                        // Also update adjacency[x][n_j]
                        if (!adjacency[x].ContainsKey(n_j))
                            adjacency[x][n_j] = new List<BFEdge>();
                        adjacency[x][n_j].AddRange(edgesIX);

                        // Remove adjacency[x][n_i] because n_i is about to vanish
                        adjacency[x].Remove(n_i);
                    }
                }

                // Remove adjacency from n_j->n_i
                if (adjacency[n_j].ContainsKey(n_i))
                    adjacency[n_j].Remove(n_i);

                // Finally, remove n_i from adjacency entirely
                adjacency.Remove(n_i);

                // Remove from active set
                ActiveVertices.Remove(n_i);
            }

            /// <summary>
            /// Delete all edges between n_i and n_j, but keep both vertices.
            /// (If n_i and n_j have multiple parallel edges, remove them all.)
            /// </summary>
            public void DeleteEdges(int n_i, int n_j)
            {
                if (adjacency[n_i].ContainsKey(n_j))
                    adjacency[n_i][n_j].Clear();
                if (adjacency[n_j].ContainsKey(n_i))
                    adjacency[n_j][n_i].Clear();
            }
        }
    }
}
