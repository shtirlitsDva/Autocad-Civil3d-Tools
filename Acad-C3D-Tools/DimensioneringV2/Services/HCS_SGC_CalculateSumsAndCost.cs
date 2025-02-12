using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Genetic;
using DimensioneringV2.GraphModel;
using DimensioneringV2.Services.SubGraphs;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class HCS_SGC_CalculateSumsAndCost
    {
        /// <summary>
        /// Calculates the cost of the chromosome's graph
        /// </summary>
        internal static double CalculateSumsAndCost(GraphChromosomeOptimized chr,
            List<(
                Func<BFEdge, dynamic> Getter,
                Action<BFEdge, dynamic> Setter)> props)
        {
            return CalculateSumsAndCost(
                chr.LocalGraph, chr.CoherencyManager.OriginalGraph, props, chr.CoherencyManager.MetaGraph);
        }
        /// <summary>
        /// Calculates the cost of the graph
        /// </summary>
        internal static double CalculateSumsAndCost(UndirectedGraph<BFNode, BFEdge> graph,
            UndirectedGraph<BFNode, BFEdge> subGraph,
            List<(
                Func<BFEdge, dynamic> Getter,
                Action<BFEdge, dynamic> Setter)> props,
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph)
        {
            var rootNode = metaGraph.GetRootForSubgraph(subGraph);

            var spt = new UndirectedGraph<BFNode, BFEdge>();
            spt.AddVertexRange(graph.Vertices);
            
            // Dijkstra's algorithm for shortest paths from the root node
            var tryGetPaths = graph.ShortestPathsDijkstra(edge => edge.Length, rootNode);

            var terminals = metaGraph.GetTerminalsForSubgraph(subGraph);

            foreach (var vertex in terminals)
            {
                if (tryGetPaths(vertex, out var path))
                {
                    foreach (var edge in path)
                    {
                        if (!spt.ContainsEdge(edge))
                        {
                            spt.AddEdge(edge);
                        }
                    }
                }
            }

            //Maybe this works?
            graph = spt;


            // Calculate the sums
            CalculateSubgraphs.BFCalcBaseSums(
                spt, rootNode, new HashSet<BFNode>(), metaGraph, props);
            // Calculate the cost
            CalculateSubgraphs.CalculateHydraulics(
                HydraulicCalculationService.Calc, spt);
            return spt.Edges.Sum(x => x.Price);
        }
    }
}
