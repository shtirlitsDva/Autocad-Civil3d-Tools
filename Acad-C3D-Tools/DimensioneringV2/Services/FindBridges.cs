using Autodesk.AutoCAD.BoundaryRepresentation;

using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphFeatures;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal static class FindBridges
    {
        /// <summary>
        /// Finds all bridge edges.
        /// </summary>
        internal static HashSet<BFEdge> DoFindThem(UndirectedGraph<BFNode, BFEdge> graph)
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
                    BridgeDfs(graph, node, node, ref cnt, low, pre, bridges);
                }
            }

            return bridges;
        }

        /// <summary>
        /// Finds all non-bridge edges.
        /// </summary>
        internal static List<BFEdge> FindNonBridges(UndirectedGraph<BFNode, BFEdge> graph)
        {
            var bridges = DoFindThem(graph);
            return graph.Edges.Where(x => !bridges.Contains(x)).ToList();
        }

        internal static void BridgeDfs(
            UndirectedGraph<BFNode, BFEdge> graph,
            BFNode u,
            BFNode v,
            ref int cnt,
            Dictionary<BFNode, int> low,
            Dictionary<BFNode, int> pre,
            HashSet<BFEdge> bridges)
        {
            cnt++;
            pre[v] = cnt;
            low[v] = pre[v];

            foreach (var edge in graph.AdjacentEdges(v))
            {
                var w = edge.Source.Equals(v) ? edge.Target : edge.Source;
                if (pre[w] == -1)
                {
                    BridgeDfs(graph, v, w, ref cnt, low, pre, bridges);

                    low[v] = Math.Min(low[v], low[w]);
                    if (low[w] == pre[w])
                    {
                        bridges.Add(edge);
                    }
                }
                else if (!w.Equals(u))
                {
                    low[v] = Math.Min(low[v], pre[w]);
                }
            }
        }

        internal static void DoMarkThem(UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
        {
            var bridges = new HashSet<EdgePipeSegment>();
            var low = new Dictionary<NodeJunction, int>();
            var pre = new Dictionary<NodeJunction, int>();
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
                    BridgeDfs(graph, node, node, ref cnt, low, pre, bridges);
                }
            }

            foreach (var bridge in bridges)
            {
                bridge.PipeSegment.IsBridge = true;
            }
        }
        internal static void BridgeDfs(
            UndirectedGraph<NodeJunction, EdgePipeSegment> graph,
            NodeJunction u,
            NodeJunction v,
            ref int cnt,
            Dictionary<NodeJunction, int> low,
            Dictionary<NodeJunction, int> pre,
            HashSet<EdgePipeSegment> bridges)
        {
            cnt++;
            pre[v] = cnt;
            low[v] = pre[v];

            foreach (var edge in graph.AdjacentEdges(v))
            {
                var w = edge.Source.Equals(v) ? edge.Target : edge.Source;
                if (pre[w] == -1)
                {
                    BridgeDfs(graph, v, w, ref cnt, low, pre, bridges);

                    low[v] = Math.Min(low[v], low[w]);
                    if (low[w] == pre[w])
                    {
                        bridges.Add(edge);
                    }
                }
                else if (!w.Equals(u))
                {
                    low[v] = Math.Min(low[v], pre[w]);
                }
            }
        }
    }
}
