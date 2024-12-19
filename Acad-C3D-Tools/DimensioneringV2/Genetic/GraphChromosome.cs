using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;

using GeneticSharp;

using Mapsui.Utilities;

using QuikGraph;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private CoherencyManager _chm;

        public UndirectedGraph<BFNode, BFEdge> LocalGraph => _localGraph;
        public List<BFEdge> RemovedEdges => _removedEdges;

        public GraphChromosome(CoherencyManager coherencyManager) 
            : base(coherencyManager.ChromosomeLength)
        {
            _chm = coherencyManager;
            _originalGraph = _chm.OriginalGraph.Copy();
            _localGraph = _chm.OriginalGraph.Copy();

            _orderedNonBridges = FindBridges.FindNonBridges(_localGraph).OrderBy(x => x.NonBridgeChromosomeIndex).ToArray();

            var random = RandomizationProvider.Current;

            BitArray bitArray;
            var randomizedIndici = 
                Enumerable.Range(0, _orderedNonBridges.Length)
                .OrderBy(x => random.GetDouble()).ToArray();

            do
            {
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

                bitArray = GetBitArray();
            }
            while (!_chm.IsUnique(bitArray));
        }

        public override IChromosome CreateNew()
        {
            return new GraphChromosome(Length, _originalGraph, _chm);
        }

        public BitArray GetBitArray()
        {
            var bitArray = new BitArray(Length);
            for (int i = 0; i < Length; i++)
            {
                bitArray[i] = (int)GetGene(i).Value == 1;
            }
            return bitArray;
        }

        public bool IsValidMutation(int index)
        {


            var tempGraph = _localGraph.Copy();
        }
    }
}