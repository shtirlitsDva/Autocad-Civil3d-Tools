using DimensioneringV2.BruteForceOptimization;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphModel
{
    internal static class MetaGraphBuilder
    {
        internal static MetaGraph<UndirectedGraph<BFNode, BFEdge>> BuildMetaGraph(
            List<UndirectedGraph<BFNode, BFEdge>> subGraphs)
        {
            var t = typeof(UndirectedGraph<BFNode, BFEdge>);

            // 1) Precompute node -> list of subgraphs that contain it
            // Flatten, group by node, and build the dictionary:
            var nodeToSubgraphs = subGraphs
                // SelectMany “flattens” each subgraph's nodes, pairing each node with its subgraph
                .SelectMany(sg => sg.Vertices.Select(node => new { Node = node, SubGraph = sg }))
                // Group by the BFNode
                .GroupBy(x => x.Node)
                // Convert each group into a dictionary entry with BFNode -> List of subgraphs
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.SubGraph).ToList()
                );

            // 2) Create our MetaGraph and initialize NodeFlags
            var metaGraph = new MetaGraph<UndirectedGraph<BFNode, BFEdge>>();

            // 3) Create a visited set for subgraphs
            var visited = new HashSet<UndirectedGraph<BFNode, BFEdge>>();

            // 4) Identify "root subgraphs" by the presence of a BFNode with IsRootNode=true
            var rootSubgraphs = subGraphs
                .Where(x => x.Vertices.Any(y => y.IsRootNode));

            // 5) Perform a DFS-like traversal on each root subgraph
            foreach (var rootSubGraph in rootSubgraphs)
            {
                // Mark the root node as a root in the subgraph, just so we don't have to do it later
                SetSubgraphNodeFlag(metaGraph, rootSubGraph, rootSubGraph.GetRoot()!, isRoot: true);

                var metaRoot = new MetaNode<UndirectedGraph<BFNode, BFEdge>>(rootSubGraph);
                metaGraph.Roots.Add(metaRoot);

                var stack = new Stack<MetaNode<UndirectedGraph<BFNode, BFEdge>>>();
                stack.Push(metaRoot);

                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    var currentSubGraph = cur.Value;

                    if (!visited.Add(currentSubGraph)) continue;

                    // For every node in current subgraph, look up other subgraphs that share it
                    foreach (var node in currentSubGraph.Vertices)
                    {
                        if (!nodeToSubgraphs.TryGetValue(node, out var connectedSubgraphs))
                            continue;

                        foreach (var nextSubGraph in connectedSubgraphs)
                        {
                            // Skip if visited or the same subgraph
                            if (visited.Contains(nextSubGraph) || nextSubGraph == currentSubGraph)
                                continue;

                            // We discovered a bridging node. Mark as leaf in current, root in next.
                            SetSubgraphNodeFlag(metaGraph, currentSubGraph, node, isLeaf: true);
                            SetSubgraphNodeFlag(metaGraph, nextSubGraph, node, isRoot: true);

                            // Create a new MetaNode and link it
                            var childNode = new MetaNode<UndirectedGraph<BFNode, BFEdge>>(nextSubGraph);
                            cur.AddChild(childNode);
                            stack.Push(childNode);
                        }
                    }
                }
            }

            return metaGraph;
        }

        private static void SetSubgraphNodeFlag(
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            UndirectedGraph<BFNode, BFEdge> subGraph,
            BFNode node,
            bool? isRoot = null,
            bool? isLeaf = null)
        {
            // Get or create the dictionary for this subgraph
            if (!metaGraph.NodeFlags.TryGetValue(subGraph, out var nodeDict))
            {
                nodeDict = new Dictionary<BFNode, SubgraphNodeMetadata>();
                metaGraph.NodeFlags[subGraph] = nodeDict;
            }

            // Get or create the SubgraphNodeMetadata for this particular node
            if (!nodeDict.TryGetValue(node, out var nodeMeta))
            {
                nodeMeta = new SubgraphNodeMetadata();
                nodeDict[node] = nodeMeta;
            }

            // Update whichever flags are provided
            if (isRoot.HasValue) nodeMeta.IsRoot = isRoot.Value;
            if (isLeaf.HasValue) nodeMeta.IsLeaf = isLeaf.Value;
        }
    }
}