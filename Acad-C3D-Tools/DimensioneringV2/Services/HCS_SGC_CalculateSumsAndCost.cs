using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.Genetic;
using DimensioneringV2.GraphModel;
using DimensioneringV2.GraphUtilities;
using DimensioneringV2.ResultCache;

using GeneticSharp;

using NorsynHydraulicCalc;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services
{
    internal class HCS_SGC_CalculateSumsAndCost
    {
        /// <summary>
        /// Calculates the cost of the chromosome's graph.
        /// Updates the chromosome's LocalGraph with the calculated results.
        /// Supports both StrictGraphChromosome and RelaxedGraphChromosome.
        /// </summary>
        internal static double CalculateSumsAndCost(
            IChromosome chromosome,
            List<SumProperty<BFEdge>> props,
            HydraulicCalculationCache<BFEdge> cache)
        {
            return chromosome switch
            {
                StrictGraphChromosome strict => CalculateSumsAndCostForStrict(strict, props, cache),
                RelaxedGraphChromosome relaxed => CalculateSumsAndCostForRelaxed(relaxed, props, cache),
                _ => throw new ArgumentException("Chromosome must be StrictGraphChromosome or RelaxedGraphChromosome!")
            };
        }

        private static double CalculateSumsAndCostForStrict(
            StrictGraphChromosome chr,
            List<SumProperty<BFEdge>> props,
            HydraulicCalculationCache<BFEdge> cache)
        {
            var r = CalculateSumsAndCost(
                chr.LocalGraph,
                chr.CoherencyManager.OriginalGraph,
                props,
                chr.CoherencyManager.MetaGraph,
                cache);

            chr.LocalGraph = r.graph;

            return r.price;
        }

        private static double CalculateSumsAndCostForRelaxed(
            RelaxedGraphChromosome chr,
            List<SumProperty<BFEdge>> props,
            HydraulicCalculationCache<BFEdge> cache)
        {
            var r = CalculateSumsAndCost(
                chr.LocalGraph,
                chr.CoherencyManager.OriginalGraph,
                props,
                chr.CoherencyManager.MetaGraph,
                cache);

            chr.LocalGraph = r.graph;

            return r.price;
        }

        /// <summary>
        /// Calculates the cost of the graph by building shortest path tree,
        /// calculating sums, and applying hydraulic calculations.
        /// </summary>
        internal static (double price, UndirectedGraph<BFNode, BFEdge> graph) CalculateSumsAndCost(
            UndirectedGraph<BFNode, BFEdge> graph,
            UndirectedGraph<BFNode, BFEdge> originalSubGraph,
            List<SumProperty<BFEdge>> props,
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            HydraulicCalculationCache<BFEdge> cache)
        {
            var rootNode = metaGraph.GetRootForSubgraph(originalSubGraph);

            // We need to isolate calculations from the graph passed here
            // We don't want to store the results in the original graph
            // We just calculate the price
            var tempGraph = graph.CopyWithNewEdges();

            // Build shortest path tree from root to all terminals
            var spt = new UndirectedGraph<BFNode, BFEdge>();
            spt.AddVertexRange(tempGraph.Vertices);
            
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

            // Calculate sums (with injected sums from child subgraphs)
            GraphSumCalculator.CalculateSums(spt, rootNode, props, metaGraph.Sums);

            // Calculate hydraulics for distribution pipes
            foreach (var edge in spt.Edges)
            {
                if (edge.SegmentType == SegmentType.Stikledning) continue;
                var result = cache.GetOrCalculate(edge);
                edge.ApplyResult(result);
            }

            return (spt.Edges.Sum(x => x.Price), spt);
        }
    }
}
