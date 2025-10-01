using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipelineNetworkSystem.QuikGraphClasses
{
    internal static class GraphBuilder
    {
        public static HashSet<AdjacencyGraph<TNode, TEdge>> BuildGraphs<TNode, TEdge>(GraphCollection ographs)
            where TNode : NodeBase
            where TEdge : EdgeBase<TNode>
        {
            var graphs = new HashSet<AdjacencyGraph<TNode, TEdge>>();
            foreach (var graph in ographs)
            {
                var krg = new AdjacencyGraph<TNode, TEdge>();

                // First create node instances while keeping track of the mapping
                var nodeMapping = new Dictionary<INode, TNode>();

                Queue<INode> queue = new Queue<INode>();
                queue.Enqueue(graph.Root);

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();

                    if (nodeMapping.ContainsKey(node)) continue;

                    TNode knode = (TNode)Activator.CreateInstance(typeof(TNode), node.Value);
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
                        TEdge krEdge = (TEdge)Activator.CreateInstance(typeof(TEdge), krNode, krChildNode);
                        krg.AddEdge(krEdge);
                    }
                }

                graphs.Add(krg);
            }

            return graphs;
        }
    }
}
