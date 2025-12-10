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
            // Support both strict and relaxed chromosome types
            var (localGraph, coherencyManager) = chromosome switch
            {
                StrictGraphChromosome strict => (strict.LocalGraph, strict.CoherencyManager),
                RelaxedGraphChromosome relaxed => (relaxed.LocalGraph, relaxed.CoherencyManager),
                _ => throw new ArgumentException("Chromosome must be StrictGraphChromosome or RelaxedGraphChromosome!")
            };
            
            // Check terminal connectivity - apply heavy penalty if disconnected
            if (!localGraph.AreTerminalNodesConnected(_chm.RootNode, _chm.Terminals))
            {
                return -double.MaxValue;
            }

            double result = HCS_SGC_CalculateSumsAndCost.CalculateSumsAndCost(
                chromosome, _props, _cache);

            return -result;
        }
    }
}
