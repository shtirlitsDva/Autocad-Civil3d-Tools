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

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static GeneticAlgorithm SetupGAAnalysis(
            UndirectedGraph<BFNode, BFEdge> graph,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props)
        {
            CoherencyManager chm = new CoherencyManager(graph);

            var population = new Population(
                50,
                200,
                new GraphChromosome(chm));

            var fitness = new GraphFitness(props);
            var selection = new EliteSelection();
            var crossover = new UniqueCrossover(chm, 0.5f);
            var mutation = new GraphMutation(chm);

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
            {
                Termination = new FitnessStagnationTermination(1000)
            };

            ga.TaskExecutor = new ParallelTaskExecutor()
            {
                MinThreads = 4,
                MaxThreads = 32
            };

            ga.MutationProbability = 0.5f;
            //ga.CrossoverProbability = 0.85f;

            return ga;
        }
    }
}
