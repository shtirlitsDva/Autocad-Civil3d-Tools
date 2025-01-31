using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphModel;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.SubGraphs
{
    internal class CalculateMetaGraphRecursively
    {
        private static MetaGraph<UndirectedGraph<BFNode, BFEdge>> _metaGraph;
        internal static void CalculateMetaGraph(
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props
            )
        {
            _metaGraph = metaGraph;

            foreach (var root in _metaGraph.Roots)
            {
                Calculate(root, props);
            }
        }
        private static void Calculate(
            MetaNode<UndirectedGraph<BFNode, BFEdge>> node,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props)
        {
            foreach (var child in node.Children) Calculate(child, props);



        }
    }
}
