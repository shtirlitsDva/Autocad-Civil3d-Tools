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

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static void CalculateGAAnalysis(
            UndirectedGraph<NodeJunction, EdgePipeSegment> graph,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props,
            Action<int, double> reportProgress,
            CancellationToken token)
        {
            UndirectedGraph<BFNode, BFEdge> gaGraph = graph.CopyToBF();
            var ga = SetupGAAnalysis(gaGraph, props);

            ga.GenerationRan += (s, e) =>
            {
                if (token.IsCancellationRequested)
                {
                    ga.Stop();
                    return;
                }

                var bestChromosome = ga.BestChromosome;
                var fitness = -bestChromosome?.Fitness ?? 0.0;
                var generation = ga.GenerationsNumber;

                // Report progress to the UI
                reportProgress(generation, fitness);
            };

            ga.TaskExecutor = new ParallelTaskExecutor()
            {
                MinThreads = 4,
                MaxThreads = 500
            };

            ga.Start();

            if (token.IsCancellationRequested)
            {
                ga.Stop();
            }

            var bestChromosome = ga.BestChromosome as GraphChromosome;

            if (bestChromosome == null)
            {
                MessageBox.Show("No valid solution found by the genetic algorithm!");
                return;
            }

            // Handle result processing for this graph
            CalculateBFCost(bestChromosome.LocalGraph, props);

            //Update the original graph with the results from the best result
            foreach (var edge in bestChromosome.LocalGraph.Edges)
            {
                edge.OriginalEdge.PipeSegment.PipeDim = edge.PipeDim;
                edge.OriginalEdge.PipeSegment.ReynoldsSupply = edge.ReynoldsSupply;
                edge.OriginalEdge.PipeSegment.ReynoldsReturn = edge.ReynoldsReturn;
                edge.OriginalEdge.PipeSegment.FlowSupply = edge.FlowSupply;
                edge.OriginalEdge.PipeSegment.FlowReturn = edge.FlowReturn;
                edge.OriginalEdge.PipeSegment.PressureGradientSupply = edge.PressureGradientSupply;
                edge.OriginalEdge.PipeSegment.PressureGradientReturn = edge.PressureGradientReturn;
                edge.OriginalEdge.PipeSegment.VelocitySupply = edge.VelocitySupply;
                edge.OriginalEdge.PipeSegment.VelocityReturn = edge.VelocityReturn;
                edge.OriginalEdge.PipeSegment.UtilizationRate = edge.UtilizationRate;
            }
        }
    }
}
