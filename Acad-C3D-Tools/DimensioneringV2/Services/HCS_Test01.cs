using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.BruteForceOptimization;

using utils = IntersectUtilities.UtilsCommon.Utils;

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
            UndirectedGraph<NodeJunction, EdgePipeSegment> graph,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props
            )
        {
            UndirectedGraph<BFNode, BFEdge> gaGraph = graph.CopyToBF();
            var bridges = FindBridges.DoFindThem(gaGraph);
            var nonBridges = FindBridges.FindNonBridges(gaGraph);

            var root = gaGraph.GetRoot();
            if (root == null)
            {
                utils.prdDbg("No root node found in the graph!");
                return;
            }
            var bfsRank = new Dictionary<BFNode, int>();
            RankNodesBFS(gaGraph, root!, bfsRank);

            //Separate nonbridge graph
            var nonBridgeGraph = new UndirectedGraph<BFNode, BFEdge>();
            foreach (var v in gaGraph.Vertices) nonBridgeGraph.AddVertex(v);
            foreach (var e in nonBridges) nonBridgeGraph.AddEdge(e);

            //Find connected elements in nonbridge graph and create partial graphs
            var subId = new Dictionary<BFNode, int>();
            var subDict = new Dictionary<int, UndirectedGraph<BFNode, BFEdge>>();
            int currId = 0;

            foreach (var node in nonBridgeGraph.Vertices)
            {
                if (!subId.ContainsKey(node))
                {
                    currId++;
                    subDict[currId] = new UndirectedGraph<BFNode, BFEdge>();
                    MarkComponent(node, currId, nonBridgeGraph, subDict[currId], subId);
                }
            }


        }

        private static void RankNodesBFS(
        UndirectedGraph<BFNode, BFEdge> graph, BFNode root,
        Dictionary<BFNode, int> rank)
        {
            var queue = new Queue<BFNode>();
            queue.Enqueue(root);
            rank[root] = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int currRank = rank[current];

                foreach (var neighbor in graph.AdjacentVertices(current)) // Simplified!
                {
                    if (!rank.ContainsKey(neighbor)) // Only visit unranked nodes
                    {
                        rank[neighbor] = currRank + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private static void MarkComponent(
        BFNode startNode, int componentId,
        UndirectedGraph<BFNode, BFEdge> gNonBridge,
        UndirectedGraph<BFNode, BFEdge> componentGraph,
        Dictionary<BFNode, int> subId)
        {
            var stack = new Stack<BFNode>();
            stack.Push(startNode);
            subId[startNode] = componentId;
            componentGraph.AddVertex(startNode);

            while (stack.Count > 0)
            {
                var top = stack.Pop();
                foreach (var e in gNonBridge.AdjacentEdges(top))
                {
                    var other = e.Source.Equals(top) ? e.Target : e.Source;
                    if (!subId.ContainsKey(other))
                    {
                        subId[other] = componentId;
                        componentGraph.AddVertex(other);
                        componentGraph.AddEdge(e);
                        stack.Push(other);
                    }
                }
            }
        }
    }
}
