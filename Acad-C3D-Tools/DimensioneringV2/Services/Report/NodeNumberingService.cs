using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models;

using QuikGraph;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Assigns stable, sequential NodeId values to all nodes in a HydraulicNetwork.
/// Algorithm: filter unused edges, enumerate root->leaf paths sorted by length (longest first),
/// number nodes along each path, skipping already-numbered nodes.
///
/// Single-network mode: IDs are "1", "2", "3"...
/// Multi-network mode:  IDs are "{graphIndex}.1", "{graphIndex}.2"... per graph.
/// </summary>
internal static class NodeNumberingService
{
    /// <summary>
    /// Assigns sequential string NodeIds across all graphs.
    /// <paramref name="orderedGraphs"/> provides the graphs in the desired ordering
    /// (typically sorted by edge count descending for report output).
    ///
    /// Single graph  -> IDs: "1", "2", "3"...
    /// Multiple graphs -> IDs: "1.1", "1.2"... for first graph, "2.1", "2.2"... for second, etc.
    /// </summary>
    internal static void AssignNodeIds(
        HydraulicNetwork hn,
        List<UndirectedGraph<NodeJunction, EdgePipeSegment>> orderedGraphs)
    {
        // Reset all existing IDs first (across ALL graphs in hn, not just orderedGraphs)
        foreach (var graph in hn.Graphs)
            foreach (var node in graph.Vertices)
                node.NodeId = "";

        bool multiNetwork = orderedGraphs.Count > 1;

        for (int i = 0; i < orderedGraphs.Count; i++)
        {
            string prefix = multiNetwork ? $"{i + 1}." : "";
            AssignNodeIdsForGraph(orderedGraphs[i], prefix);
        }
    }

    private static void AssignNodeIdsForGraph(
        UndirectedGraph<NodeJunction, EdgePipeSegment> graph, string prefix)
    {
        // 1. Filter: keep only edges where buildings are actually supplied
        //    (edges with 0 were discarded by the genetic algorithm)
        var activeEdges = graph.Edges
            .Where(e => e.PipeSegment.NumberOfBuildingsSupplied > 0)
            .ToList();

        if (activeEdges.Count == 0) return;

        // 2. Build adjacency map from active edges only
        var adjacency = new Dictionary<NodeJunction, List<EdgePipeSegment>>();
        foreach (var edge in activeEdges)
        {
            if (!adjacency.TryGetValue(edge.Source, out var sourceList))
            {
                sourceList = new List<EdgePipeSegment>();
                adjacency[edge.Source] = sourceList;
            }
            sourceList.Add(edge);

            if (!adjacency.TryGetValue(edge.Target, out var targetList))
            {
                targetList = new List<EdgePipeSegment>();
                adjacency[edge.Target] = targetList;
            }
            targetList.Add(edge);
        }

        // 3. Find root node (supply point)
        var root = adjacency.Keys.FirstOrDefault(n => n.IsRootNode);
        if (root == null) return;

        // 4. Enumerate all root->terminal paths via DFS
        var allPaths = new List<(List<NodeJunction> Nodes, double Length)>();
        var currentPath = new List<NodeJunction>();
        DfsEnumeratePaths(root, null, adjacency, currentPath, allPaths, 0);

        // 5. Sort by total path length in meters (longest first)
        allPaths.Sort((a, b) => b.Length.CompareTo(a.Length));

        // 6. Assign IDs: process longest path first, skip already-numbered nodes
        int nextSeq = 1;
        foreach (var (nodes, _) in allPaths)
        {
            foreach (var node in nodes)
            {
                if (string.IsNullOrEmpty(node.NodeId))
                    node.NodeId = $"{prefix}{nextSeq++}";
            }
        }
    }

    /// <summary>
    /// DFS from current node, collecting all paths to leaf nodes.
    /// Works on tree-structured graphs (no cycles expected after filtering).
    /// </summary>
    private static void DfsEnumeratePaths(
        NodeJunction current,
        NodeJunction? parent,
        Dictionary<NodeJunction, List<EdgePipeSegment>> adjacency,
        List<NodeJunction> currentPath,
        List<(List<NodeJunction> Nodes, double Length)> allPaths,
        double currentLength)
    {
        currentPath.Add(current);

        bool isLeaf = true;
        foreach (var edge in adjacency[current])
        {
            var neighbor = ReferenceEquals(edge.Source, current) ? edge.Target : edge.Source;
            if (ReferenceEquals(neighbor, parent)) continue;

            isLeaf = false;
            DfsEnumeratePaths(
                neighbor, current, adjacency, currentPath, allPaths,
                currentLength + edge.PipeSegment.Length);
        }

        if (isLeaf)
        {
            allPaths.Add((new List<NodeJunction>(currentPath), currentLength));
        }

        currentPath.RemoveAt(currentPath.Count - 1);
    }
}
