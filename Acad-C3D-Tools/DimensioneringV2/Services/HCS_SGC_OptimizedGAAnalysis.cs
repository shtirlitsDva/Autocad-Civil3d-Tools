using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;

using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.Genetic;
using DimensioneringV2.GraphModel;
using DimensioneringV2.ResultCache;
using DimensioneringV2.UI;

using GeneticSharp;

using QuikGraph;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static void CalculateOptimizedGAAnalysis(
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            UndirectedGraph<BFNode, BFEdge> subGraph,
            UndirectedGraph<BFNode, BFEdge> seed,
            List<SumProperty<BFEdge>> props,
            GeneticAlgorithmCalculationViewModel gaVM,
            CancellationToken token,
            HydraulicCalculationCache<BFEdge> cache)
        {
            var ga = SetupOptimizedGAAnalysis(metaGraph, subGraph, seed, props, cache);

            ga.GenerationRan += (s, e) =>
            {
                var bestChromosome = ga.BestChromosome;
                var fitness = -bestChromosome?.Fitness ?? 0.0;
                var generation = ga.GenerationsNumber;

                // Report progress to the UI
                GeneticOptimizedReportingContext.VM.Dispatcher.Invoke(() =>
                {
                    gaVM.ReportProgress(generation, fitness);
                });

                if (token.IsCancellationRequested)
                {
                    ga.Stop();
                    return;
                }
            };

            ga.Start();

            var bestChromosome = ga.BestChromosome as GraphChromosome;

            if (bestChromosome == null)
            {
                MessageBox.Show("No valid solution found by the genetic algorithm!");
                return;
            }

            // Handle result processing for this graph
            var rootNode = metaGraph.GetRootForSubgraph(subGraph);

            HCS_SGC_CalculateSumsAndCost.CalculateSumsAndCost(bestChromosome, props, cache);
            
            // Update the original graph with the results from the best result
            foreach (var edge in bestChromosome.LocalGraph.Edges)
            {
                edge.PushAllResults();
            }
        }
    }
}
