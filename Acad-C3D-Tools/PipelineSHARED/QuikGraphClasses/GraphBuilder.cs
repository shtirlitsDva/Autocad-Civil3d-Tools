using IntersectUtilities.UtilsCommon.Graphs;

using QuikGraph;

using System;
using System.Collections.Generic;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.PipelineNetworkSystem.QuikGraphClasses
{
    internal static class GraphBuilder
    {
        public static HashSet<AdjacencyGraph<TNode, TEdge>> BuildGraphs<T, TNode, TEdge>(
            GraphCollection<T> ographs)
            where TNode : NodeBase
            where TEdge : EdgeBase<TNode>
        {
            var graphs = new HashSet<AdjacencyGraph<TNode, TEdge>>();
            foreach (var graph in ographs)
            {
                var krg = new AdjacencyGraph<TNode, TEdge>();

                // First create node instances while keeping track of the mapping
                var nodeMapping = new Dictionary<Node<T>, TNode>();

                Queue<Node<T>> queue = new Queue<Node<T>>();
                queue.Enqueue(graph.Root);

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();

                    if (nodeMapping.ContainsKey(node)) continue;

                    TNode? knode = Activator.CreateInstance(typeof(TNode), node.Value) as TNode;
                    if (knode == null) { prdDbg(
                        $"Failed to create instance of: {typeof(TNode)}"); continue; }

                    if (node.Parent == null) knode.Root = true;
                    krg.AddVertex(knode);
                    nodeMapping.Add(node, knode);

                    foreach (var child in node.Children)
                    {
                        queue.Enqueue(child);
                    }
                }

                // Now create edges
                foreach (var node in nodeMapping.Keys)
                {
                    var krNode = nodeMapping[node];
                    foreach (var child in node.Children)
                    {
                        var krChildNode = nodeMapping[child];
                        TEdge? krEdge = Activator.CreateInstance(typeof(TEdge), krNode, krChildNode) as TEdge;
                        if (krEdge == null) {prdDbg(
                            $"Failed to create instance of: {typeof(TNode)}"); continue;}
                        krg.AddEdge(krEdge);
                    }
                }

                graphs.Add(krg);
            }

            return graphs;
        }
    }
}
