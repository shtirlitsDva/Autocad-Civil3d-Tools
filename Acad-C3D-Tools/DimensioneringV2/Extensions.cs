using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.Geometry;

using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.Services.GDALClient;
using DimensioneringV2.SteinerTreeProblem;

using QuikGraph;
using QuikGraph.Algorithms.Observers;
using QuikGraph.Algorithms.Search;

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
        #region Misc
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
        public const double g = 9.80665; // m/s^2
        public static double BarToMVS(this double bar) => bar * 10.19716;
        public static double BarToMVS(this double bar, double rho) => (bar * 100000) / (rho * g);
        public static double MVStoBar(this double mVS) => mVS / 10.19716;
        public static double MVStoBar(this double mVS, double rho) => (mVS * rho * g) / 100000;
        public static double PaToBar(this double pa) => pa / 100000;
        public static double BarToPa(this double bar) => bar * 100000;
        public static double PaToMVS(this double pa) => pa.PaToBar().BarToMVS();
        public static double PaToMVS(this double pa, double rho) => pa / (rho * g);
        public static double MVStoPa(this double mVS, double rho) => mVS * (rho * g);
        #endregion

        #region Graph extensions
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
            foreach (EdgePipeSegment edge in graph.Edges)
            {
                var bfEdge = new BFEdge(nodeMap[edge.Source], nodeMap[edge.Target], edge);
                bfGraph.AddEdge(bfEdge);
            }

            return bfGraph;
        }
        public static UndirectedGraph<BFNode, BFEdge> CopyToBFConditional(
            this UndirectedGraph<NodeJunction, EdgePipeSegment> graph,
            Predicate<EdgePipeSegment> predicate)
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
            foreach (EdgePipeSegment edge in graph.Edges.Where(x => predicate(x)))
            {
                var bfEdge = new BFEdge(nodeMap[edge.Source], nodeMap[edge.Target], edge);
                bfGraph.AddEdge(bfEdge);
            }

            return bfGraph;
        }
        public static UndirectedGraph<BFNode, BFEdge> CopyWithNewVerticesAndEdges(this UndirectedGraph<BFNode, BFEdge> graph)
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
            // Copy edges with all calculated properties preserved
            foreach (var edge in graph.Edges)
            {
                var bfEdge = new BFEdge(nodeMap[edge.Source], nodeMap[edge.Target], edge);
                bfEdge.NonBridgeChromosomeIndex = edge.NonBridgeChromosomeIndex;                
                bfGraph.AddEdge(bfEdge);
            }

            return bfGraph;
        }
        public static UndirectedGraph<BFNode, BFEdge> CopyWithNewEdges(this UndirectedGraph<BFNode, BFEdge> graph)
        {
            var graphCopy = new UndirectedGraph<BFNode, BFEdge>();

            // Copy edges with all calculated properties preserved
            foreach (var edge in graph.Edges)
            {
                var edgeCopy = new BFEdge(edge);
                edgeCopy.NonBridgeChromosomeIndex = edge.NonBridgeChromosomeIndex;
                graphCopy.AddVerticesAndEdge(edgeCopy);
            }

            return graphCopy;
        }
        public static void AddEdgeCopy(this UndirectedGraph<BFNode, BFEdge> graph, BFEdge edge)
        {
            var newEdge = new BFEdge(edge.Source, edge.Target, edge);
            newEdge.NonBridgeChromosomeIndex = edge.NonBridgeChromosomeIndex;
            graph.AddVerticesAndEdge(newEdge);
        }        
        public static void InitNonBridgeChromosomeIndex(this UndirectedGraph<BFNode, BFEdge> graph)
        {
            var bridges = FindBridges.DoFindThem(graph);

            int index = 0;
            foreach (var edge in graph.Edges)
            {
                if (bridges.Contains(edge)) continue;
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
        public static bool AreTerminalNodesConnected(
            this UndirectedGraph<BFNode, BFEdge> graph,
            BFNode root, HashSet<BFNode> terminals)
        {
            var visited = new HashSet<BFNode>();
            var stack = new Stack<BFNode>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (!visited.Add(node)) continue;

                foreach (var neighbor in graph.AdjacentVertices(node))
                    if (!visited.Contains(neighbor)) stack.Push(neighbor);
            }

            return visited.IsSupersetOf(terminals);
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
        /// <summary>
        /// Checks if an edge is a bridge by testing connectivity without it.
        /// Much more efficient than computing ALL bridges when checking a single edge.
        /// O(V+E) worst case, but only traverses until target is found.
        /// </summary>
        public static bool IsBridgeEdge(this UndirectedGraph<BFNode, BFEdge> graph, BFEdge edge)
        {
            // An edge is a bridge if removing it disconnects its endpoints.
            // We check this with BFS from source to target, ignoring the edge.
            var source = edge.Source;
            var target = edge.Target;

            var visited = new HashSet<BFNode>();
            var queue = new Queue<BFNode>();
            queue.Enqueue(source);
            visited.Add(source);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node == target) return false; // Found path without using the edge

                foreach (var adjEdge in graph.AdjacentEdges(node))
                {
                    // Skip the edge we're testing
                    if (adjEdge == edge) continue;

                    var neighbor = adjEdge.GetOtherVertex(node);
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return true; // No path found = edge is a bridge
        }

        public static BFNode? GetRoot(this UndirectedGraph<BFNode, BFEdge> graph)
        {
            // Find the root node
            var rootNode = graph.Vertices.FirstOrDefault(x => x.IsRootNode);
            if (rootNode != null) return rootNode;
            else return null;
        }
        public static double RootElevation(this UndirectedGraph<BFNode, BFEdge> graph, BFNode root)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (root == null) throw new ArgumentNullException(nameof(root));

            // Ensure root has exactly one incident edge
            var edge = graph.AdjacentEdges(root).SingleOrDefault();
            if (edge == null)
                throw new InvalidOperationException("Root node has no incident edge.");

            // Orient profile: root → neighbor
            var neighbor = edge.Source.Equals(root) ? edge.Target : edge.Source;

            var cache = edge.OriginalEdge.PipeSegment.Elevations;
            var prof = cache.GetProfile(
                root.OriginalNodeJunction.Location.Coordinate,
                neighbor.OriginalNodeJunction.Location.Coordinate);

            if (prof.Count == 0)
                throw new InvalidOperationException("No elevation samples available for root edge.");

            return prof[0].Elevation;
        }
        /// <summary>
        /// Returns oriented elevation profiles along the BFS path root→target.
        /// Each tuple is (AnalysisFeature, oriented profile) in traversal order.
        /// </summary>
        public static IEnumerable<(AnalysisFeature feature, List<ElevationSample> profile)>
            OrientedProfiles(this UndirectedGraph<BFNode, BFEdge> graph, BFNode root, BFNode target)
        {
            if (graph == null) throw new System.ArgumentNullException(nameof(graph));
            if (root == null) throw new System.ArgumentNullException(nameof(root));
            if (target == null) throw new System.ArgumentNullException(nameof(target));
            if (Equals(root, target)) yield break;

            // BFS + predecessor recorder
            var bfs = new UndirectedBreadthFirstSearchAlgorithm<BFNode, BFEdge>(graph);
            var rec = new UndirectedVertexPredecessorRecorderObserver<BFNode, BFEdge>();
            rec.Attach(bfs);
            bfs.Compute(root);

            // Reconstruct vertex path root→target
            var vs = ReconstructVertexPath(root, target, rec.VerticesPredecessors);
            if (vs.Count < 2) yield break;

            // Walk consecutive vertex pairs; orient each edge’s profile
            for (int i = 0; i < vs.Count - 1; i++)
            {
                var a = vs[i];
                var b = vs[i + 1];

                // find the undirected edge between a and b
                var edge = graph.AdjacentEdges(a).First(e => e.Source.Equals(b) || e.Target.Equals(b));

                var feature = edge.OriginalEdge.PipeSegment;                 // AnalysisFeature
                var cache = feature.Elevations;                            // ElevationProfileCache
                var prof = cache.GetProfile(
                    a.Location.Coordinate, b.Location.Coordinate)     // orient by node vectors (EPSG:3857)
                .ToList();                                // materialize

                yield return (feature, prof);
            }

            List<BFNode> ReconstructVertexPath(
                BFNode root, BFNode target, IDictionary<BFNode, BFEdge> pred)
            {
                var vs = new List<BFNode>();
                var v = target;
                while (!Equals(v, root))
                {
                    if (!pred.TryGetValue(v, out var pe)) return new List<BFNode>(); // unreachable
                    vs.Add(v);
                    v = pe.Source.Equals(v) ? pe.Target : pe.Source; // step toward root
                }
                vs.Add(root);
                vs.Reverse();
                return vs;
            }
        }
        #endregion

        #region Misc
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
        #endregion
    }
}
