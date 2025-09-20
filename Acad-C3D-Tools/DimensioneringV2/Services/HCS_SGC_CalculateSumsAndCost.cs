using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Genetic;
using DimensioneringV2.GraphModel;
using DimensioneringV2.ResultCache;
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
                Action<BFEdge, dynamic> Setter)> props,
            HydraulicCalculationCache cache)
        {

            var r = CalculateSumsAndCost(
                chr.LocalGraph,
                chr.CoherencyManager.OriginalGraph,
                props,
                chr.CoherencyManager.MetaGraph,
                cache);

            //Assume that NonBridgeChromosomeIndex has been preserved in the graph
            //even if we have new copies of the edges
            //But testing showed that this actually prevented convergence
            //chr.UpdateChromosome(r.graph);

            chr.LocalGraph = r.graph;

            return r.price;
        }
        /// <summary>
        /// Calculates the cost of the graph
        /// </summary>
        internal static (double price, UndirectedGraph<BFNode, BFEdge> graph) CalculateSumsAndCost(
            UndirectedGraph<BFNode, BFEdge> graph,
            UndirectedGraph<BFNode, BFEdge> originalSubGraph,
            List<(
                Func<BFEdge, dynamic> Getter,
                Action<BFEdge, dynamic> Setter)> props,
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            HydraulicCalculationCache cache)
        {
            var rootNode = metaGraph.GetRootForSubgraph(originalSubGraph);

            //We need to isolate calculations from the graph passed here
            //We don't want to store the results in the original graph
            //We just calculate the price
            var tempGraph = graph.CopyWithNewEdges();

            var spt = new UndirectedGraph<BFNode, BFEdge>();
            spt.AddVertexRange(tempGraph.Vertices);
            
            // Dijkstra's algorithm for shortest paths from the root node
            var tryGetPaths = tempGraph.ShortestPathsDijkstra(edge => edge.Length, rootNode);

            var terminals = metaGraph.GetTerminalsForSubgraph(originalSubGraph);

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

            // Calculate the sums
            CalculateSubgraphs.BFCalcBaseSums(
                spt, rootNode, new HashSet<BFNode>(), metaGraph, props);
            // Calculate the cost
            CalculateSubgraphs.CalculateHydraulics(spt, cache);

            return (spt.Edges.Sum(x => x.Price), spt);
        }
    }
}