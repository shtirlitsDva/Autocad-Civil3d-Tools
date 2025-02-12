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

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static GeneticAlgorithm SetupOptimizedGAAnalysis(
            MetaGraph<UndirectedGraph<BFNode, BFEdge>> metaGraph,
            UndirectedGraph<BFNode, BFEdge> subGraph,
            UndirectedGraph<BFNode, BFEdge> seed,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props)
        {
            CoherencyManagerOptimized chm = new CoherencyManagerOptimized(metaGraph, subGraph, seed);

            var population = new Population(
                50,
                200,
                new GraphChromosomeOptimized(chm));

            var fitness = new GraphFitnessOptimized(chm, props);
            var selection = new EliteSelection();
            var crossover = new UniqueCrossoverOptimized(chm, 0.5f);
            var mutation = new GraphMutationOptimized(chm);

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
            {
                Termination = new FitnessStagnationTermination(300)
            };

            ga.TaskExecutor = new ParallelTaskExecutor()
            {
                MinThreads = 4,
                MaxThreads = 16
            };

            ga.MutationProbability = 0.95f;
            //ga.CrossoverProbability = 0.85f;

            return ga;
        }
    }
}
