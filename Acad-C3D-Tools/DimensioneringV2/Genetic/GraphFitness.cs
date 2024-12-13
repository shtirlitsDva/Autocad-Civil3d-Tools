using DimensioneringV2.BruteForceOptimization;

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
        private readonly List<BFEdge> _nonBridges;
        private readonly List<(Func<BFEdge, dynamic> Getter, Action<BFEdge, dynamic> Setter)> _props;

        public GraphFitness(
            UndirectedGraph<BFNode, BFEdge> originalGraph,
            List<BFEdge> nonBridges,
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

            // Ensure graph connectivity
            if (!candidateGraph.IsConnected())
            {
                return double.MaxValue; // Penalize disconnected graphs
            }

            return CalculateBFCost(candidateGraph, _props);
        }
    }
}
