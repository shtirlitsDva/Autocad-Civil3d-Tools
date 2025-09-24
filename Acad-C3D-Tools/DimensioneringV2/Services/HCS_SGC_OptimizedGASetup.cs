using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.BruteForceOptimization;

using utils = IntersectUtilities.UtilsCommon.Utils;

using IntersectUtilities.UtilsCommon;

using DimensioneringV2.Genetic;
using GeneticSharp;
using System.Collections;
using System.Collections.Concurrent;
using Mapsui.Utilities;
using DimensioneringV2.GraphModel;
using DimensioneringV2.ResultCache;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static GeneticAlgorithm SetupOptimizedGAAnalysis(
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            UndirectedGraph<BFNode, BFEdge> subGraph,
            UndirectedGraph<BFNode, BFEdge> seed,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props,
            HydraulicCalculationCache cache)
        {
            CoherencyManager chm = new CoherencyManager(metaGraph, subGraph, seed);

            var population = new Population(
                50,
                200,
                new GraphChromosome(chm));

            var fitness = new GraphFitness(chm, props, cache);
            var selection = new EliteSelection();
            var crossover = new UniqueCrossover(chm, 0.5f);
            var mutation = new GraphMutation(chm);

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
            {
                Termination = new FitnessStagnationTermination(
                    HydraulicSettingsService.Instance.Settings.NumberOfGSLUToEnd)
            };

            //ga.TaskExecutor = new ParallelTaskExecutor()
            //{
            //    MinThreads = 1,
            //    MaxThreads = Environment.ProcessorCount
            //};

            ga.TaskExecutor = new TplTaskExecutor();

            //ga.TaskExecutor = new LinearTaskExecutor();

            ga.MutationProbability = 0.95f;
            //ga.CrossoverProbability = 0.85f;

            return ga;
        }
    }
}
