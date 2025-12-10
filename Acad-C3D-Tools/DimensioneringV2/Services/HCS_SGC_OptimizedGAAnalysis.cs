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
        internal static UndirectedGraph<BFNode, BFEdge>? CalculateOptimizedGAAnalysis(
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
                // Check cancellation FIRST - don't do any work if cancelled
                if (token.IsCancellationRequested)
                {
                    ga.Stop();
                    return;
                }

                var bestChromosome = ga.BestChromosome;
                var fitness = -bestChromosome?.Fitness ?? 0.0;
                var generation = ga.GenerationsNumber;

                // Report progress to the UI (non-blocking)
                GeneticOptimizedReportingContext.VM.Dispatcher.BeginInvoke(() =>
                {
                    gaVM.ReportProgress(generation, fitness);
                });
            };

            ga.Start();

            var bestChromosome = ga.BestChromosome;

            if (bestChromosome == null)
            {
                MessageBox.Show("No valid solution found by the genetic algorithm!");
                return null;
            }

            // Handle result processing for this graph
            var rootNode = metaGraph.GetRootForSubgraph(subGraph);

            HCS_SGC_CalculateSumsAndCost.CalculateSumsAndCost(bestChromosome, props, cache);

            // Extract LocalGraph from either chromosome type
            var localGraph = bestChromosome switch
            {
                StrictGraphChromosome strict => strict.LocalGraph,
                RelaxedGraphChromosome relaxed => relaxed.LocalGraph,
                _ => throw new InvalidOperationException("Unknown chromosome type")
            };

            return localGraph;
        }
    }
}
