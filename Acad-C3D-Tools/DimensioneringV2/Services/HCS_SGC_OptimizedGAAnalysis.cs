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

            // Get the CoherencyManager from the chromosome
            var chm = GetCoherencyManagerFromChromosome(bestChromosome);
            if (chm == null)
            {
                MessageBox.Show("Could not extract CoherencyManager from chromosome!");
                return null;
            }

            // Always rebuild graph from genes - works with any chromosome type
            var localGraph = chm.RebuildGraphFromChromosome(bestChromosome);

            // Calculate final results with the graph
            var result = HCS_SGC_CalculateSumsAndCost.CalculateSumsAndCostWithGraph(
                localGraph, subGraph, props, metaGraph, cache);

            return result.graph;
        }

        /// <summary>
        /// Extracts the CoherencyManager from any supported chromosome type.
        /// </summary>
        private static CoherencyManager? GetCoherencyManagerFromChromosome(IChromosome chromosome)
        {
            return chromosome switch
            {
                StrictGraphChromosome strict => strict.CoherencyManager,
                RelaxedGraphChromosome relaxed => relaxed.CoherencyManager,
                _ => null
            };
        }
    }
}
