using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphFeatures;
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
    internal class GraphChromosome : BinaryChromosomeBase
    {
        private readonly UndirectedGraph<BFNode, BFEdge> _originalGraph;
        private readonly UndirectedGraph<BFNode, BFEdge> _localGraph;
        private readonly List<BFEdge> _removedEdges = new List<BFEdge>();
        private readonly BFEdge[] _orderedNonBridges;

        public UndirectedGraph<BFNode, BFEdge> LocalGraph => _localGraph;
        public List<BFEdge> RemovedEdges => _removedEdges;

        public GraphChromosome(int chromosomeLength, UndirectedGraph<BFNode, BFEdge> graph) : base(chromosomeLength)
        {
            _originalGraph = graph.Copy();
            _localGraph = graph.Copy();
            _orderedNonBridges = FindBridges.FindNonBridges(_localGraph).OrderBy(x => x.ChromosomeIndex).ToArray();

            var random = RandomizationProvider.Current;
            var randomizedIndici = 
                Enumerable.Range(0, _orderedNonBridges.Length)
                .OrderBy(x => random.GetDouble()).ToArray();

            var test = GetGenes();
            var range = Enumerable.Range(0, chromosomeLength).ToArray();

            for (int i = 0; i < randomizedIndici.Length; i++)
            {
                // Determine if the edge should be removed
                bool removeEdge = random.GetDouble() >= 0.5;
                var edge = _orderedNonBridges[randomizedIndici[i]];

                if (removeEdge && !_localGraph.IsBridgeEdge(edge))
                {
                    _localGraph.RemoveEdge(edge);
                    _removedEdges.Add(edge);
                    ReplaceGene(i, new Gene(1));
                }
                else
                {
                    ReplaceGene(i, new Gene(0));
                }
            }
        }

        public override IChromosome CreateNew()
        {
            return new GraphChromosome(Length, _originalGraph);
        }
    }
}