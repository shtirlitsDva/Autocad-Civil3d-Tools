using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Services;

using GeneticSharp;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Genetic
{
    internal class GraphFitness : IFitness
    {
        private readonly List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> _props;

        public GraphFitness(
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props
            ) => _props = props;

        public double Evaluate(IChromosome chromosome)
        {
            if (chromosome is not GraphChromosome graphChromosome)
                throw new ArgumentException("Chromosome is not of type GraphChromosome");
            
            if (!graphChromosome.LocalGraph.AreBuildingNodesConnected())
            {
                return -double.MaxValue;
            }

            return -HydraulicCalculationsService.CalculateBFCost(graphChromosome.LocalGraph, _props);
        }
    }
}
