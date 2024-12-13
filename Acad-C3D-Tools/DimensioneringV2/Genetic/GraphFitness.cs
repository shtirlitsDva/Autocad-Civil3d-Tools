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
        private readonly UndirectedGraph<BFNode, BFEdge> _originalGraph;
        private readonly IEnumerable<BFEdge> _nonBridges;
        private readonly List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> _props;

        public GraphFitness(
            UndirectedGraph<BFNode, BFEdge> originalGraph,
            IEnumerable<BFEdge> nonBridges,
            List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> props
            )
        {
            _originalGraph = originalGraph;
            _nonBridges = nonBridges;
            _props = props;
        }

        public double Evaluate(IChromosome chromosome)
        {
            if (chromosome is not GraphChromosome graphChromosome)
                throw new ArgumentException("Chromosome is not of type GraphChromosome");
            
            var nonSelectedEdges = graphChromosome.GetNonSelectedEdges();
            var candidateGraph = _originalGraph.Copy();
            candidateGraph.RemoveEdges(nonSelectedEdges);

            if (!candidateGraph.IsConnected())
            {
                return double.MaxValue;
            }

            return -HydraulicCalculationsService.CalculateBFCost(candidateGraph, _props);
        }
    }
}
