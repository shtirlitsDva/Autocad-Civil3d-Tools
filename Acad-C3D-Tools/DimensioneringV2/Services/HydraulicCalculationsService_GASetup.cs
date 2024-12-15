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

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static GeneticAlgorithm SetupGAAnalysis(
            UndirectedGraph<BFNode, BFEdge> graph,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props)
        {
            var bridges = FindBridges.DoFindThem(graph);
            var nonBridges = graph.Edges.Where(x => !bridges.Contains(x));

            graph.InitChromosomeIndex();

            var population = new Population(
                nonBridges.Count(),
                nonBridges.Count() * 2,
                new GraphChromosome(nonBridges.Count(), graph.Copy()));

            var fitness = new GraphFitness(props);
            var selection = new EliteSelection();
            var crossover = new CycleCrossover();
            var mutation = new FlipBitMutation();

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
            {
                Termination = new FitnessStagnationTermination(1000)
            };

            return ga;
        }
    }
}
