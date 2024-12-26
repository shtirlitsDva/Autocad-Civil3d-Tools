using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.Geometry;

using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.SteinerTreeProblem;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2
{
    internal static class Extensions
    {
        public static Point2D To2D(this Point3d pt)
        {
            return new Point2D(pt.X, pt.Y);
        }

        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                                .Cast<DescriptionAttribute>()
                                .FirstOrDefault();
            return attribute?.Description ?? value.ToString();
        }

        public static STP ToSTP(this UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
        {
            var stp = new STP();

            #region Renumber all nodes
            //Number all nodes
            var node = graph.Vertices.Where(x => x.IsRootNode).FirstOrDefault();
            //If no root node is found, the first node is selected
            if (node == null) node = graph.Vertices.First();

            HashSet<NodeJunction> visited = new HashSet<NodeJunction>();
            Stack<NodeJunction> stack = new Stack<NodeJunction>();
            stack.Push(node);

            int nodeNumber = 0;
            while (stack.Count > 0)
            {
                node = stack.Pop();
                if (visited.Contains(node)) continue;
                visited.Add(node);
                nodeNumber++;
                node.STP_Node = nodeNumber;

                foreach (var v in graph.AdjacentVertices(node))
                {
                    stack.Push(v);
                }
            }
            #endregion

            //Add all nodes and terminals
            foreach (var v in graph.Vertices)
            {
                stp.AddNode(v);
                if (graph.AdjacentEdges(v).Count() == 1 &&
                    (int)graph.AdjacentEdges(v).First().PipeSegment.SegmentType == 1)
                {
                    stp.AddTerminal(v.STP_Node);
                }
            }
            //Add all edges
            foreach (var e in graph.Edges)
            {
                stp.AddEdge(e.Source.STP_Node, e.Target.STP_Node, (int)e.PipeSegment.Length);
            }

            return stp;
        }

        public static UndirectedGraph<BFNode, BFEdge> CopyToBF(this UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
        {
            var bfGraph = new UndirectedGraph<BFNode, BFEdge>();

            var nodeMap = new Dictionary<NodeJunction, BFNode>();

            // Copy nodes
            foreach (NodeJunction node in graph.Vertices)
            {
                var bfNode = new BFNode(node);
                nodeMap[node] = bfNode;
                bfGraph.AddVertex(bfNode);
            }

            // Copy edges
            foreach (var edge in graph.Edges)
            {
                var bfEdge = new BFEdge(nodeMap[edge.Source], nodeMap[edge.Target], edge);
                bfGraph.AddEdge(bfEdge);
            }

            return bfGraph;
        }

        public static UndirectedGraph<BFNode, BFEdge> Copy(this UndirectedGraph<BFNode, BFEdge> graph)
        {
            var bfGraph = new UndirectedGraph<BFNode, BFEdge>();
            var nodeMap = new Dictionary<BFNode, BFNode>();
            // Copy nodes
            foreach (var node in graph.Vertices)
            {
                var bfNode = new BFNode(node.OriginalNodeJunction);
                nodeMap[node] = bfNode;
                bfGraph.AddVertex(bfNode);
            }
            // Copy edges
            foreach (var edge in graph.Edges)
            {
                var bfEdge = new BFEdge(nodeMap[edge.Source], nodeMap[edge.Target], edge.OriginalEdge);
                bfEdge.NonBridgeChromosomeIndex = edge.NonBridgeChromosomeIndex;
                bfGraph.AddEdge(bfEdge);
            }

            return bfGraph;
        }
        public static void AddEdgeCopy(this UndirectedGraph<BFNode, BFEdge> graph, BFEdge edge)
        {
            var source = graph.Vertices.FirstOrDefault(x => x.OriginalNodeJunction == edge.Source.OriginalNodeJunction);
            var target = graph.Vertices.FirstOrDefault(x => x.OriginalNodeJunction == edge.Target.OriginalNodeJunction);
            graph.AddEdge(new BFEdge(source, target, edge.OriginalEdge));
        }
        public static void InitNonBridgeChromosomeIndex(this UndirectedGraph<BFNode, BFEdge> graph)
        {
            var brdiges = FindBridges.DoFindThem(graph);

            int index = 0;
            foreach (var edge in graph.Edges)
            {
                if (brdiges.Contains(edge)) continue;
                edge.NonBridgeChromosomeIndex = index;
                index++;
            }
        }
        public static void RemoveSelectedEdges(this UndirectedGraph<BFNode, BFEdge> graph, IEnumerable<BFEdge> edges)
        {
            foreach (var edge in edges)
            {
                var edgeToRemove = graph.Edges.FirstOrDefault(
                    x => x.Source.OriginalNodeJunction == edge.Source.OriginalNodeJunction &&
                    x.Target.OriginalNodeJunction == edge.Target.OriginalNodeJunction);

                if (edgeToRemove == null) throw new Exception("Edge not found in graph!");

                graph.RemoveEdge(edgeToRemove);
            }
        }
        public static bool AreBuildingNodesConnected(this UndirectedGraph<BFNode, BFEdge> graph)
        {
            // Find the root node
            var rootNode = graph.Vertices.FirstOrDefault(x => x.IsRootNode);
            if (rootNode == null) return false;

            var visited = new HashSet<BFNode>();
            var stack = new Stack<BFNode>();
            stack.Push(rootNode);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (!visited.Add(node)) continue;

                foreach (var neighbor in graph.AdjacentVertices(node))
                {
                    if (!visited.Contains(neighbor))
                        stack.Push(neighbor);
                }
            }

            // Verify all leaf nodes with IsBuildingNode == true are visited
            return graph.Vertices
                .Where(v => graph.AdjacentEdges(v).Count() == 1 && v.IsBuildingNode) // Filter building leaves
                .All(visited.Contains); // Ensure all are connected
        }
        private static void TraverseGraph<TVertex, TEdge>(this 
            UndirectedGraph<TVertex, TEdge> graph, TVertex node, HashSet<TVertex> visited) where TEdge : IEdge<TVertex>
        {
            if (visited.Contains(node)) return;
            visited.Add(node);

            foreach (var neighbor in graph.AdjacentVertices(node))
            {
                if (!visited.Contains(neighbor))
                {
                    TraverseGraph(graph, neighbor, visited);
                }
            }
        }
        public static bool IsBridgeEdge(this UndirectedGraph<BFNode, BFEdge> graph, BFEdge edge)
        {
            var bridges = new HashSet<BFEdge>();
            var low = new Dictionary<BFNode, int>();
            var pre = new Dictionary<BFNode, int>();
            foreach (var node in graph.Vertices)
            {
                low[node] = -1;
                pre[node] = -1;
            }

            int cnt = 0;
            foreach (var node in graph.Vertices)
            {
                if (pre[node] == -1)
                {
                    FindBridges.BridgeDfs(graph, node, node, ref cnt, low, pre, bridges);
                }
            }

            return bridges.Contains(edge);
        }
        public static bool IsBridgeEdge(this UndirectedGraph<BFNode, BFEdge> graph, int index)
        {
            var bridges = new HashSet<BFEdge>();
            var low = new Dictionary<BFNode, int>();
            var pre = new Dictionary<BFNode, int>();
            foreach (var node in graph.Vertices)
            {
                low[node] = -1;
                pre[node] = -1;
            }

            int cnt = 0;
            foreach (var node in graph.Vertices)
            {
                if (pre[node] == -1)
                {
                    FindBridges.BridgeDfs(graph, node, node, ref cnt, low, pre, bridges);
                }
            }

            return bridges.Any(x => x.NonBridgeChromosomeIndex == index);
        }
        public static void RemoveEdgeByNonBridgeIndex(this UndirectedGraph<BFNode, BFEdge> graph, int index)
        {
            var edge = graph.Edges.FirstOrDefault(x => x.NonBridgeChromosomeIndex == index);
            if (edge == null) throw new Exception("Edge not found in graph!");
            graph.RemoveEdge(edge);
        }

        public static class ThreadSafeRandom
        {
            [ThreadStatic] private static Random Local;

            public static Random ThisThreadsRandom
            {
                get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
            }
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
