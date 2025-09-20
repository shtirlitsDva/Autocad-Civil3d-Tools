using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphModel;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.SubGraphs
{
    internal class CalculateMetaGraphRecursively
    {
        private MetaGraph<UndirectedGraph<BFNode, BFEdge>> _metaGraph;

        public CalculateMetaGraphRecursively(MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph)
        {
            this._metaGraph = metaGraph;
        }

        internal void CalculateBaseSumsForMetaGraph(
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props)
        {
            foreach (var root in _metaGraph.Roots)
            {
                Calculate(root, props);
            }
        }
        private void Calculate(
            MetaNode<UndirectedGraph<BFNode, BFEdge>> node,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props)
        {
            foreach (var child in node.Children) Calculate(child, props);

            var graph = node.Value;

            if (!_metaGraph.NodeFlags.ContainsKey(graph))
                _metaGraph.NodeFlags[graph] = new();

            var nodeFlags = _metaGraph.NodeFlags[graph];

            bool isLeaf(BFNode node)
            {
                if (nodeFlags.TryGetValue(node, out var value)) return value.IsLeaf;
                return false;
            }

            BFNode? rootNode = nodeFlags.FirstOrDefault(x => x.Value.IsRoot).Key;
            if (rootNode == null)
                throw new Exception("Root node not found.");

            var shortestPathTree = new UndirectedGraph<BFNode, BFEdge>();
            shortestPathTree.AddVertexRange(graph.Vertices);

            var tryGetPaths = graph.ShortestPathsDijkstra(
                edge => edge.Length, rootNode);

            var query = graph.Vertices.Where(
                x => x.IsBuildingNode || isLeaf(x)
                );

            foreach (var vertex in query)
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

            var visited = new HashSet<BFNode>();
            var sum = CalculateSubgraphs.BFCalcBaseSums(shortestPathTree, rootNode, visited, _metaGraph, props);
            _metaGraph.Sums[rootNode] = sum;
        }
    }
}