using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.GraphModel;
using DimensioneringV2.GraphUtilities;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.MetaGraphForSubGraphs
{
    internal class CalculateMetaGraphRecursively
    {
        private MetaGraph<UndirectedGraph<BFNode, BFEdge>> _metaGraph;

        public CalculateMetaGraphRecursively(MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph)
        {
            _metaGraph = metaGraph;
        }

        /// <summary>
        /// Calculates base sums for all subgraphs in the metagraph.
        /// Processes children first (bottom-up), storing sums at bridge nodes
        /// so they can be injected when processing parent subgraphs.
        /// </summary>
        internal void CalculateBaseSumsForMetaGraph(List<SumProperty<BFEdge>> props)
        {
            foreach (var root in _metaGraph.Roots)
            {
                Calculate(root, props);
            }
        }

        private void Calculate(
            MetaNode<UndirectedGraph<BFNode, BFEdge>> node,
            List<SumProperty<BFEdge>> props)
        {
            // Process children first (bottom-up traversal)
            foreach (var child in node.Children) Calculate(child, props);

            var graph = node.Value;

            if (!_metaGraph.NodeFlags.ContainsKey(graph))
                _metaGraph.NodeFlags[graph] = new();

            var nodeFlags = _metaGraph.NodeFlags[graph];

            bool isLeaf(BFNode n)
            {
                if (nodeFlags.TryGetValue(n, out var value)) return value.IsLeaf;
                return false;
            }

            BFNode? rootNode = nodeFlags.FirstOrDefault(x => x.Value.IsRoot).Key;
            if (rootNode == null)
                throw new Exception("Root node not found in subgraph.");

            // Build shortest path tree from root to all terminals (buildings + leaf bridge nodes)
            var shortestPathTree = new UndirectedGraph<BFNode, BFEdge>();
            shortestPathTree.AddVertexRange(graph.Vertices);

            var tryGetPaths = graph.ShortestPathsDijkstra(edge => edge.Length, rootNode);

            var terminals = graph.Vertices.Where(x => x.IsBuildingNode || isLeaf(x));

            foreach (var vertex in terminals)
            {
                if (tryGetPaths(vertex, out var path))
                {
                    foreach (var edge in path)
                    {
                        if (!shortestPathTree.ContainsEdge(edge))
                        {
                            shortestPathTree.AddVerticesAndEdge(edge);
                        }
                    }
                }
            }

            // Calculate sums recursively, injecting pre-calculated sums from child subgraphs
            var sums = GraphSumCalculator.CalculateSums(
                shortestPathTree, rootNode, props, _metaGraph.Sums);

            // Store this subgraph's total sums at root for parent subgraph injection
            _metaGraph.Sums[rootNode] = sums.ToList();
        }
    }
}
