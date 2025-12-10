using System;
using System.Collections.Generic;

using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.Genetic;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.GraphModel;
using DimensioneringV2.ResultCache;

using GeneticSharp;

using QuikGraph;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static GeneticAlgorithm SetupOptimizedGAAnalysis(
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            UndirectedGraph<BFNode, BFEdge> subGraph,
            UndirectedGraph<BFNode, BFEdge> seed,
            List<SumProperty<BFEdge>> props,
            HydraulicCalculationCache<BFEdge> cache)
        {
            CoherencyManager chm = new CoherencyManager(metaGraph, subGraph, seed);

            var population = new Population(
                10,
                20,
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

            int threadCount = Environment.ProcessorCount;
            ga.TaskExecutor = new TplTaskExecutor();
            //ga.TaskExecutor = new ParallelTaskExecutor
            //{
            //    MinThreads = threadCount,
            //    MaxThreads = threadCount
            //};
            //ga.TaskExecutor = new LinearTaskExecutor();
            ga.MutationProbability = 0.95f;

            return ga;
        }        
    }
}