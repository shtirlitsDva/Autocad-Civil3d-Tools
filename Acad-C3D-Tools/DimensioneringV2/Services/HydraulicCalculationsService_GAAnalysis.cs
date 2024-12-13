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
            UndirectedGraph<NodeJunction, EdgePipeSegment> graph,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props)
        {
            UndirectedGraph<BFNode, BFEdge> bfGraph = graph.CopyToBF();

            var bridges = FindBridges.DoFindThem(bfGraph);
            var nonBridges = bfGraph.Edges.Where(x => !bridges.Contains(x));

            var chromosomeLength = nonBridges.Count();
            var fitness = new GraphFitness(bfGraph, nonBridges, props);
            var chromosome = new GraphChromosome(chromosomeLength, nonBridges.ToList());

            var population = new Population(50, 100, chromosome);
            var selection = new TournamentSelection();
            var crossover = new UniformCrossover();
            var mutation = new FlipBitMutation();

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
            {
                Termination = new FitnessStagnationTermination(50)
            };

            return ga;
        }
    }
}
