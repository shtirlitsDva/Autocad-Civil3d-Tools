using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.BruteForceOptimization;

using static DimensioneringV2.Utils;

using DimensioneringV2.Genetic;
using GeneticSharp;
using System.Threading;
using DotSpatial.Projections;
using System.Windows;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static void Test01(
            UndirectedGraph<NodeJunction, EdgePipeSegment> originalGraph,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props
            )
        {
            UndirectedGraph<BFNode, BFEdge> graph = originalGraph.CopyToBF();
            var bridges = FindBridges.DoFindThem(graph);
            var nonBridges = FindBridges.FindNonBridges(graph);

            var root = graph.GetRoot();
            if (root == null)
            {
                prtDbg("No root node found in the graph!");
                return;
            }
            var bfsRank = AssignBfsRank(graph, root);

            // 3) Identify connected non-bridge components
            var subGraphs = new List<UndirectedGraph<BFNode, BFEdge>>();
            var visited = new HashSet<BFNode>();
            foreach (var node in graph.Vertices)
            {
                if (!visited.Contains(node))
                {
                    var subGraph = new UndirectedGraph<BFNode, BFEdge>();
                    FloodNonBridge(graph, node, nonBridges, visited, subGraph);
                    if (subGraph.VertexCount > 0) subGraphs.Add(subGraph);
                }
            }

            // 4) Process bridge edges and assign to downstream subgraph
            AssignBridgeEdges(graph, subGraphs, bridges, bfsRank);


            //Print number of subgraphs
            //and number of edges in each subgraph sorted
            prtDbg(
                $"Number of subgraphs: {subGraphs.Count}"
                );
        }

        private static Dictionary<BFNode, int> AssignBfsRank(
        UndirectedGraph<BFNode, BFEdge> graph, BFNode root)
        {
            var queue = new Queue<BFNode>();
            var rank = new Dictionary<BFNode, int> { [root] = 0 };
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var e in graph.AdjacentEdges(current))
                {
                    var neighbor = e.Source == current ? e.Target : e.Source;
                    if (!rank.ContainsKey(neighbor))
                    {
                        rank[neighbor] = rank[current] + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return rank;
        }

        private static void FloodNonBridge(
            UndirectedGraph<BFNode, BFEdge> graph, BFNode start, HashSet<BFEdge> nonBridges,
            HashSet<BFNode> visited, UndirectedGraph<BFNode, BFEdge> subGraph)
        {
            var stack = new Stack<BFNode>();
            stack.Push(start);
            visited.Add(start);
            subGraph.AddVertex(start);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                foreach (var edge in graph.AdjacentEdges(node))
                {
                    if (nonBridges.Contains(edge))
                    {
                        var neighbor = edge.Source == node ? edge.Target : edge.Source;
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            subGraph.AddVertex(neighbor);
                            subGraph.AddEdge(edge);
                            stack.Push(neighbor);
                        }
                    }
                }
            }
        }

        private static void AssignBridgeEdges(
            UndirectedGraph<BFNode, BFEdge> graph,
            List<UndirectedGraph<BFNode, BFEdge>> subGraphs,
            HashSet<BFEdge> bridges,
            Dictionary<BFNode, int> bfsRank)
        {
            var bridgeGraph = new UndirectedGraph<BFNode, BFEdge>();
            foreach (var edge in bridges)
            { 
                bridgeGraph.AddVertex(edge.Source);
                bridgeGraph.AddVertex(edge.Target);
                bridgeGraph.AddEdge(edge); 
            }

            var visited = new HashSet<BFNode>();
            foreach (var node in bridgeGraph.Vertices)
            {
                if (!visited.Contains(node))
                {
                    var compNodes = new List<BFNode>();
                    var compEdges = new List<BFEdge>();
                    CollectBridgeComponent(bridgeGraph, node, visited, compNodes, compEdges);

                    // Attach to the most downstream subgraph
                    var bestSub = subGraphs.OrderByDescending(sg =>
                            sg.Vertices.Where(v => compNodes.Contains(v))
                            .Select(v => bfsRank[v])
                            .DefaultIfEmpty(-1)
                            .Max())
                        .FirstOrDefault();

                    if (bestSub != null)
                    {
                        foreach (var v in compNodes) bestSub.AddVertex(v);
                        foreach (var e in compEdges) bestSub.AddEdge(e);
                    }
                }
            }
        }

        private static void CollectBridgeComponent(
            UndirectedGraph<BFNode, BFEdge> graph, BFNode start, HashSet<BFNode> visited,
            List<BFNode> nodes, List<BFEdge> edges)
        {
            var stack = new Stack<BFNode>();
            stack.Push(start);
            visited.Add(start);
            nodes.Add(start);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                foreach (var edge in graph.AdjacentEdges(node))
                {
                    edges.Add(edge);
                    var neighbor = edge.Source == node ? edge.Target : edge.Source;
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        nodes.Add(neighbor);
                        stack.Push(neighbor);
                    }
                }
            }
        }
    }
}
