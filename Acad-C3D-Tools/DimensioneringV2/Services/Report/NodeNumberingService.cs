using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models;

using QuikGraph;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Assigns stable, sequential NodeId values to all nodes in a HydraulicNetwork.
/// Algorithm: filter unused edges, enumerate root→leaf paths sorted by length (longest first),
/// number nodes along each path, skipping already-numbered nodes.
/// </summary>
internal static class NodeNumberingService
{
    /// <summary>
    /// Assigns 1-based sequential NodeIds across all graphs in the network.
    /// Call after calculation completes so IDs are stable and persistable.
    /// </summary>
    internal static void AssignNodeIds(HydraulicNetwork hn)
    {
        // Reset all existing IDs first
        foreach (var graph in hn.Graphs)
            foreach (var node in graph.Vertices)
                node.NodeId = -1;

        int nextId = 1;
        foreach (var graph in hn.Graphs)
        {
            nextId = AssignNodeIdsForGraph(graph, nextId);
        }
    }

    private static int AssignNodeIdsForGraph(
        UndirectedGraph<NodeJunction, EdgePipeSegment> graph, int startId)
    {
        // 1. Filter: keep only edges where buildings are actually supplied
        //    (edges with 0 were discarded by the genetic algorithm)
        var activeEdges = graph.Edges
            .Where(e => e.PipeSegment.NumberOfBuildingsSupplied > 0)
            .ToList();

        if (activeEdges.Count == 0) return startId;

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
        if (root == null) return startId;

        // 4. Enumerate all root→terminal paths via DFS
        var allPaths = new List<(List<NodeJunction> Nodes, double Length)>();
        var currentPath = new List<NodeJunction>();
        DfsEnumeratePaths(root, null, adjacency, currentPath, allPaths, 0);

        // 5. Sort by total path length in meters (longest first)
        allPaths.Sort((a, b) => b.Length.CompareTo(a.Length));

        // 6. Assign IDs: process longest path first, skip already-numbered nodes
        int nextId = startId;
        foreach (var (nodes, _) in allPaths)
        {
            foreach (var node in nodes)
            {
                if (node.NodeId == -1)
                    node.NodeId = nextId++;
            }
        }

        return nextId;
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
