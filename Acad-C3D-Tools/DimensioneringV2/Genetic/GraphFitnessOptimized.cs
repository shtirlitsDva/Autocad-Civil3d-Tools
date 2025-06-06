﻿using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphModel;
using DimensioneringV2.ResultCache;
using DimensioneringV2.Services;
using DimensioneringV2.Services.SubGraphs;

using DotSpatial.Projections.Transforms;

using GeneticSharp;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DimensioneringV2.Genetic
{
    internal class GraphFitnessOptimized : IFitness
    {
        private readonly List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> _props;
        private readonly CoherencyManagerOptimized _chm;
        private readonly HydraulicCalculationCache _cache;

        public GraphFitnessOptimized(
            CoherencyManagerOptimized coherencyManager,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props,
            HydraulicCalculationCache cache)
        { _props = props; _chm = coherencyManager; _cache = cache; }

        public double Evaluate(IChromosome chromosome)
        {
            if (chromosome is not GraphChromosomeOptimized graphChromosome)
                throw new ArgumentException("Chromosome is not of type GraphChromosomeOptimized!");
            
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