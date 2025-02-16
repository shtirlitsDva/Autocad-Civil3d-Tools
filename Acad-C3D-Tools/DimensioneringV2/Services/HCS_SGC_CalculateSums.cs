using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphModel;

using NorsynHydraulicCalc;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.SubGraphs
{
    internal class CalculateSubgraphs
    {
        internal static List<dynamic> BFCalcBaseSums(
        UndirectedGraph<BFNode, BFEdge> graph,
        BFNode node,
        HashSet<BFNode> visited, 
        MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
        List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props)
        {
            if (visited.Contains(node)) return props.Select(_ => (dynamic)0).ToList();

            visited.Add(node);

            List<dynamic> totalSums = props.Select(_ => (dynamic)0).ToList();

            // Traverse downstream nodes recursively
            foreach (var edge in graph.AdjacentEdges(node))
            {
                var neighbor = edge.GetOtherVertex(node);
                var downstreamSums = BFCalcBaseSums(
                    graph, neighbor, visited, metaGraph, props);

                for (int i = 0; i < props.Count; i++)
                {
                    totalSums[i] += downstreamSums[i];
                }

                for (int i = 0; i < props.Count; i++)
                {
                    var (getter, setter) = props[i];
                    setter(edge, downstreamSums[i]);
                }
            }

            //If this is a leaf node, set the number of buildings supplied to the connected value
            if (totalSums.All(sum => sum == 0) && graph.AdjacentEdges(node).Count() == 1)
            {
                for (int i = 0; i < props.Count; i++)
                {
                    var (getter, setter) = props[i];
                    var value = getter(graph.AdjacentEdges(node).First());
                    totalSums[i] = value;
                    setter(graph.AdjacentEdges(node).First(), value);
                }
            }

            //Inject sums from a connected graph (if any)
            if (metaGraph.Sums.ContainsKey(node))
            {
                var connectedSums = metaGraph.Sums[node];
                for (int i = 0; i < props.Count; i++)
                {
                    totalSums[i] += connectedSums[i];
                }
            }

            return totalSums;
        }

        /// <summary>
        /// Calculates the hydraulic properties of the edges in the graph
        /// </summary>
        internal static void CalculateHydraulics(
            HydraulicCalc hc,
            UndirectedGraph<BFNode, BFEdge> graph)
        {
            //Avoid oversubscription of the cpu
            //Parallel.ForEach(graph.Edges, edge =>
            foreach (var edge in graph.Edges)
            {
                var result = hc.CalculateHydraulicSegment(edge);
                edge.PipeDim = result.Dim;
                edge.ReynoldsSupply = result.ReynoldsSupply;
                edge.ReynoldsReturn = result.ReynoldsReturn;
                edge.FlowSupply = result.FlowSupply;
                edge.FlowReturn = result.FlowReturn;
                edge.PressureGradientSupply = result.PressureGradientSupply;
                edge.PressureGradientReturn = result.PressureGradientReturn;
                edge.VelocitySupply = result.VelocitySupply;
                edge.VelocityReturn = result.VelocityReturn;
                edge.UtilizationRate = result.UtilizationRate;
            }
            //);
        }
    }
}