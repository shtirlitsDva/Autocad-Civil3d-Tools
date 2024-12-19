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
        private UndirectedGraph<BFNode, BFEdge> _localGraph;
        private readonly HashSet<int> _removedEdges = new HashSet<int>();
        private CoherencyManager _chm;

        public UndirectedGraph<BFNode, BFEdge> LocalGraph => _localGraph;
        public HashSet<int> RemovedEdges => _removedEdges;

        public GraphChromosome(CoherencyManager coherencyManager) 
            : base(coherencyManager.ChromosomeLength)
        {
            _chm = coherencyManager;
            _localGraph = _chm.OriginalGraph.Copy();

            var random = RandomizationProvider.Current;

            BitArray bitArray;
            var randomizedIndici = 
                Enumerable.Range(0, _chm.ChromosomeLength)
                .OrderBy(x => random.GetDouble()).ToArray();

            do
            {
                for (int i = 0; i < randomizedIndici.Length; i++)
                {
                    // Determine if the edge should be removed
                    bool removeEdge = random.GetDouble() >= 0.5;
                    var edge = _localGraph.Edges.FirstOrDefault(x => x.NonBridgeChromosomeIndex == randomizedIndici[i]);

                    if (edge != null && removeEdge && !_localGraph.IsBridgeEdge(edge))
                    {
                        _localGraph.RemoveEdge(edge);
                        _removedEdges.Add(randomizedIndici[i]);
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
            return new GraphChromosome(_chm);
        }

        public void ResetChromosome()
        {
            _localGraph = _chm.OriginalGraph.Copy();
            _removedEdges.Clear();
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

        public bool TryMutate(int index)
        {
            int curValue = (int)GetGene(index).Value;

            //case 0 (means add edge, this can be done at any time):
            if (curValue == 0) 
            { 
                _removedEdges.Remove(index);
                _localGraph.AddEdgeCopy(_chm.OriginalNonBridgeEdgeFromIndex(index));
                return true; 
            }

            //case 1 (means remove edge, needs to be valid non-edge at current configuration):
            if (!_localGraph.IsBridgeEdge(index)) 
            { 
                _localGraph.RemoveEdgeByNonBridgeIndex(index);
                _removedEdges.Add(index);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// This must be done on a Reset Chromosome
        /// </summary>
        public void ReplaceGraphChromosomeGene(int index, Gene gene)
        {
            if (index < 0 || index >= this.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "There is no Gene on index {0} to be replaced.".With(index));
            }

            int geneValue = (int)gene.Value;

            if (geneValue == 0)
            {
                _removedEdges.Add(index);
                _localGraph.RemoveEdgeByNonBridgeIndex(index);
            }
            else if (geneValue == 1)
            {
            }
            ReplaceGene(index, gene);
        }
    }
}