using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.BruteForceOptimization;

using static DimensioneringV2.Utils;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static List<UndirectedGraph<BFNode, BFEdge>> CreateSubGraphs(
            UndirectedGraph<BFNode, BFEdge> graph)
        {
            var bridges = FindBridges.DoFindThem(graph);
            var nonBridges = FindBridges.FindNonBridges(graph);

            var nonBridgeSubs = BuildSubgraphs(graph, nonBridges);
            var bridgeSubs = BuildSubgraphs(graph, bridges);

            var root = graph.GetRoot();
            if (root == null)
            {
                prtDbg("No root node found in the graph!");
                return null;
            }
            
            var bfsRank = AssignBfsRank(graph, root);

            AttachBridgeSubgraphs(nonBridgeSubs, bridgeSubs, bfsRank);

            //Print number of subgraphs
            //and number of edges in each subgraph sorted
            prtDbg(
                $"Number of subgraphs: {nonBridgeSubs.Count}"
                );

            int graphIndex = 0;
            foreach (var sub in nonBridgeSubs)
            {
                graphIndex++;
                foreach (var edge in sub.Edges) 
                    edge.SubGraphId = graphIndex;
            }

            return nonBridgeSubs;
        }

        private static List<UndirectedGraph<BFNode, BFEdge>> BuildSubgraphs(
            UndirectedGraph<BFNode, BFEdge> fullGraph,
            IEnumerable<BFEdge> edgesToKeep)
        {
            // 1) Create a new graph that has the same vertices but only the selected edges
            var g = new UndirectedGraph<BFNode, BFEdge>();
            
            foreach (var e in edgesToKeep)
            {
                if (!g.ContainsVertex(e.Source)) g.AddVertex(e.Source);
                if (!g.ContainsVertex(e.Target)) g.AddVertex(e.Target);
                g.AddEdge(e);
            }

            // 2) Find connected components of g using BFS/DFS
            var visited = new HashSet<BFNode>();
            var result = new List<UndirectedGraph<BFNode, BFEdge>>();

            foreach (var node in g.Vertices)
            {
                if (!visited.Contains(node))
                {
                    // BFS/DFS to collect this component
                    var componentNodes = new List<BFNode>();
                    var componentEdges = new List<BFEdge>();
                    var queue = new Queue<BFNode>();
                    visited.Add(node);
                    queue.Enqueue(node);
                    componentNodes.Add(node);

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        // For each edge in g, restricted to 'current'
                        foreach (var edge in g.AdjacentEdges(current))
                        {
                            componentEdges.Add(edge);
                            var neighbor = edge.GetOtherVertex(current);
                            if (!visited.Contains(neighbor))
                            {
                                visited.Add(neighbor);
                                queue.Enqueue(neighbor);
                                componentNodes.Add(neighbor);
                            }
                        }
                    }

                    // Build a subgraph
                    var subG = new UndirectedGraph<BFNode, BFEdge>();
                    foreach (var n in componentNodes)
                        subG.AddVertex(n);
                    foreach (var e in componentEdges.Distinct()) // might have duplicates
                        if (!subG.ContainsEdge(e)) subG.AddEdge(e);

                    result.Add(subG);
                }
            }

            return result;
        }

        private static Dictionary<BFNode, int> AssignBfsRank(UndirectedGraph<BFNode, BFEdge> graph, BFNode root)
        {
            var rank = new Dictionary<BFNode, int>();
            var queue = new Queue<BFNode>();

            // Initialize
            rank[root] = 0;
            queue.Enqueue(root);

            // Standard BFS
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var edge in graph.AdjacentEdges(current))
                {
                    BFNode neighbor = (edge.Source == current) ? edge.Target : edge.Source;
                    if (!rank.ContainsKey(neighbor))
                    {
                        rank[neighbor] = rank[current] + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return rank;
        }

        private static void AttachBridgeSubgraphs(
            List<UndirectedGraph<BFNode, BFEdge>> nonBridgeSubs,
            List<UndirectedGraph<BFNode, BFEdge>> bridgeSubs,
            Dictionary<BFNode, int> bfsRank)
        {
            // We'll define a helper to get the "minimum BFS rank" of a subgraph
            int GetMinRank(UndirectedGraph<BFNode, BFEdge> sg)
                => sg.Vertices.Min(node => bfsRank.TryGetValue(node, out var r) ? r : int.MaxValue);

            // For each *bridge* subgraph:
            foreach (var bSub in bridgeSubs)
            {
                // Find all *non-bridge* subgraphs that share at least 1 node
                // i.e. intersection of bSub.Vertices & nbSub.Vertices is not empty
                var candidates = nonBridgeSubs
                    .Where(nb => nb.Vertices.Any(v => bSub.Vertices.Contains(v)))
                    .ToList();

                if (candidates.Count == 0)
                {
                    // No overlap => isolated bridge subgraph
                    // New subgraph is created
                    nonBridgeSubs.Add(bSub);
                }
                else if (candidates.Count == 1)
                {
                    // Exactly one subgraph connected => attach directly
                    MergeBridgeIntoNonBridge(candidates[0], bSub);
                }
                else
                {
                    // More than one => pick the one with the *lowest BFS rank* among its nodes
                    var chosen = candidates
                        .OrderBy(GetMinRank)  // smallest BFS rank
                        .First();

                    // Merge 
                    MergeBridgeIntoNonBridge(chosen, bSub);
                }
            }
        }

        // Merge all nodes/edges from 'bridgeSub' into 'nonBridgeSub'
        private static void MergeBridgeIntoNonBridge(
            UndirectedGraph<BFNode, BFEdge> nonBridgeSub,
            UndirectedGraph<BFNode, BFEdge> bridgeSub)
        {
            foreach (var node in bridgeSub.Vertices)
                if (!nonBridgeSub.ContainsVertex(node))
                    nonBridgeSub.AddVertex(node);

            foreach (var edge in bridgeSub.Edges)
                if (!nonBridgeSub.ContainsEdge(edge))
                    nonBridgeSub.AddEdge(edge);
        }
    }
}
