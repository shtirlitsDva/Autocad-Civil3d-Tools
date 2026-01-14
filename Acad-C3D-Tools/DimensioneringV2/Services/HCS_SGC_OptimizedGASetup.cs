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
            HydraulicCalculationCache<BFEdge> flCache,
            ClientCalculationCache<BFEdge>? slCache = null)
        {
            var gaSettings = GASettingsService.Instance.Settings;
            CoherencyManager chm = new CoherencyManager(metaGraph, subGraph, seed);

            // Create chromosome based on settings
            IChromosome adamChromosome = gaSettings.ChromosomeType switch
            {
                ChromosomeType.Strict => new StrictGraphChromosome(chm),
                ChromosomeType.Relaxed => new RelaxedGraphChromosome(chm),
                _ => new StrictGraphChromosome(chm)
            };

            var population = new Population(
                gaSettings.PopulationMinSize,
                gaSettings.PopulationMaxSize,
                adamChromosome)
            {
                GenerationStrategy = new PerformanceGenerationStrategy()
            };

            var fitness = new GraphFitness(chm, props, flCache, slCache);
            var selection = CreateSelection(gaSettings);
            var crossover = CreateCrossover(gaSettings, chm);
            var mutation = CreateMutation(gaSettings);

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
            {
                Termination = CreateTermination(gaSettings),
                Reinsertion = CreateReinsertion(gaSettings)
            };

            ga.TaskExecutor = CreateTaskExecutor(gaSettings);
            ga.MutationProbability = gaSettings.MutationProbability;
            ga.CrossoverProbability = (float)gaSettings.CrossoverProbability;

            return ga;
        }

        private static ISelection CreateSelection(GASettings settings)
        {
            return settings.SelectionType switch
            {
                SelectionType.Elite => new EliteSelection(),
                SelectionType.RouletteWheel => new RouletteWheelSelection(),
                SelectionType.StochasticUniversalSampling => new StochasticUniversalSamplingSelection(),
                SelectionType.Tournament => new TournamentSelection(settings.TournamentSize),
                SelectionType.Truncation => new TruncationSelection(),
                _ => new EliteSelection()
            };
        }

        private static ICrossover CreateCrossover(GASettings settings, CoherencyManager chm)
        {
            return settings.CrossoverType switch
            {
                CrossoverType.OnePoint => new OnePointCrossover(),
                CrossoverType.TwoPoint => new TwoPointCrossover(),
                CrossoverType.Uniform => new UniformCrossover(settings.UniformCrossoverMixProbability),
                CrossoverType.ThreeParent => new ThreeParentCrossover(),
                CrossoverType.StrictUnique => new StrictUniqueCrossover(chm, settings.StrictUniqueCrossoverMixProbability),
                _ => new UniformCrossover(0.5f)
            };
        }

        private static IMutation CreateMutation(GASettings settings)
        {
            return settings.MutationType switch
            {
                MutationType.FlipBit => new FlipBitMutation(),
                MutationType.StrictGraph => new StrictGraphMutation(),
                _ => new FlipBitMutation()
            };
        }

        private static IReinsertion CreateReinsertion(GASettings settings)
        {
            return settings.ReinsertionType switch
            {
                ReinsertionType.Elitist => new ElitistReinsertion(),
                ReinsertionType.FitnessBased => new FitnessBasedReinsertion(),
                ReinsertionType.Pure => new PureReinsertion(),
                ReinsertionType.Uniform => new UniformReinsertion(),
                _ => new ElitistReinsertion()
            };
        }

        private static ITermination CreateTermination(GASettings settings)
        {
            return settings.TerminationType switch
            {
                TerminationType.GenerationNumber => new GenerationNumberTermination(settings.GenerationNumberTerminationCount),
                TerminationType.TimeEvolving => new TimeEvolvingTermination(TimeSpan.FromSeconds(settings.TimeEvolvingTerminationSeconds)),
                TerminationType.FitnessStagnation => new FitnessStagnationTermination(settings.FitnessStagnationTerminationCount),
                TerminationType.FitnessThreshold => new FitnessThresholdTermination(settings.FitnessThresholdTerminationValue),
                _ => new FitnessStagnationTermination(100)
            };
        }

        private static ITaskExecutor CreateTaskExecutor(GASettings settings)
        {
            return settings.TaskExecutorType switch
            {
                TaskExecutorType.Tpl => new TplTaskExecutor(),
                TaskExecutorType.Parallel => new ParallelTaskExecutor
                {
                    MinThreads = settings.ParallelTaskExecutorMinThreads,
                    MaxThreads = settings.ParallelTaskExecutorMaxThreads
                },
                TaskExecutorType.Linear => new LinearTaskExecutor(),
                _ => new TplTaskExecutor()
            };
        }
    }
}