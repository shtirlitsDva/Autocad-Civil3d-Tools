using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.BruteForceOptimization;

using utils = IntersectUtilities.UtilsCommon.Utils;

using DimensioneringV2.Genetic;
using GeneticSharp;
using System.Threading;
using DotSpatial.Projections;
using System.Windows;
using DimensioneringV2.UI;
using DimensioneringV2.GraphModel;
using DimensioneringV2.Services.SubGraphs;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static void CalculateOptimizedGAAnalysis(
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            UndirectedGraph<BFNode, BFEdge> subGraph,
            UndirectedGraph<BFNode, BFEdge> seed,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props,
            GeneticAlgorithmCalculationViewModel gaVM,
            CancellationToken token)
        {
            var ga = SetupOptimizedGAAnalysis(metaGraph, subGraph, seed, props);

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

            //if (token.IsCancellationRequested)
            //{
            //    ga.Stop();
            //}

            var bestChromosome = ga.BestChromosome as GraphChromosomeOptimized;

            if (bestChromosome == null)
            {
                MessageBox.Show("No valid solution found by the genetic algorithm!");
                return;
            }

            // Handle result processing for this graph
            var visited = new HashSet<BFNode>();
            var rootNode = metaGraph.GetRootForSubgraph(subGraph);

            HCS_SGC_CalculateSumsAndCost.CalculateSumsAndCost(bestChromosome, props);
            
            //Update the original graph with the results from the best result
            foreach (var edge in bestChromosome.LocalGraph.Edges)
            {
                edge.PushAllResults();
            }
        }
    }
}
