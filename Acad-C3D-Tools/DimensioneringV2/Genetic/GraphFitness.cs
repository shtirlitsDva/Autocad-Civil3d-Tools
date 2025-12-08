using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.ResultCache;
using DimensioneringV2.Services;

using GeneticSharp;

using System;
using System.Collections.Generic;

namespace DimensioneringV2.Genetic
{
    internal class GraphFitness : IFitness
    {
        private readonly List<SumProperty<BFEdge>> _props;
        private readonly CoherencyManager _chm;
        private readonly HydraulicCalculationCache<BFEdge> _cache;

        public GraphFitness(
            CoherencyManager coherencyManager,
            List<SumProperty<BFEdge>> props,
            HydraulicCalculationCache<BFEdge> cache)
        {
            _props = props;
            _chm = coherencyManager;
            _cache = cache;
        }

        public double Evaluate(IChromosome chromosome)
        {
            if (chromosome is not GraphChromosome graphChromosome)
                throw new ArgumentException("Chromosome is not of type GraphChromosome!");

            if (!graphChromosome.LocalGraph.AreTerminalNodesConnected(
                _chm.RootNode, _chm.Terminals))
            {
                return -double.MaxValue;
            }

            double result = HCS_SGC_CalculateSumsAndCost.CalculateSumsAndCost(
                graphChromosome, _props, _cache);

            return -result;
        }
    }
}
