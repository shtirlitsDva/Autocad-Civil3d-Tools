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
    /// <summary>
    /// 1 = edge is off, 0 = edge is on
    /// </summary>
    internal class GraphChromosome : BinaryChromosomeBase
    {
        private UndirectedGraph<BFNode, BFEdge> _localGraph;
        private readonly HashSet<int> _removedEdges = new HashSet<int>();
        private CoherencyManager _chm;

        public UndirectedGraph<BFNode, BFEdge> LocalGraph => _localGraph;
        public HashSet<int> RemovedEdges => _removedEdges;
        public CoherencyManager CoherencyManager => _chm;

        public GraphChromosome(CoherencyManager coherencyManager) : base(coherencyManager.ChromosomeLength)
        {
            _chm = coherencyManager;
            _localGraph = _chm.OriginalGraph.CopyWithNewVerticesAndEdges();

            var random = RandomizationProvider.Current;

            //BitArray bitArray;

            do
            {
                var randomizedIndici =
                Enumerable.Range(0, _chm.ChromosomeLength)
                .OrderBy(x => random.GetDouble()).ToArray();

                ResetChromosome();

                for (int i = 0; i < randomizedIndici.Length; i++)
                {
                    int rIdx = randomizedIndici[i];

                    // Determine if the edge should be removed
                    bool removeEdge = random.GetDouble() >= 0.5;
                    var edge = _localGraph.Edges.FirstOrDefault(x => x.NonBridgeChromosomeIndex == rIdx);

                    if (edge != null && removeEdge && !_localGraph.IsBridgeEdge(edge))
                    {
                        _localGraph.RemoveEdge(edge);
                        _removedEdges.Add(rIdx);
                        ReplaceGene(rIdx, new Gene(1));
                    }
                    else
                    {
                        ReplaceGene(rIdx, new Gene(0));
                    }
                }

                //bitArray = GetBitArray();
            }
            while (!_localGraph.AreBuildingNodesConnected()); //&& !_chm.IsUnique(bitArray));
        }

        public override IChromosome CreateNew()
        {
            return new GraphChromosome(_chm);
        }

        public void ResetChromosome()
        {
            _localGraph = _chm.OriginalGraph.CopyWithNewVerticesAndEdges();
            _removedEdges.Clear();
            for (int i = 0; i < Length; i++)
            {
                ReplaceGene(i, new Gene(0));
            }
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

            //current value 0 means edge is on
            //case 0 (means mutates to 1 -> remove edge):
            if (curValue == 0 && !_localGraph.IsBridgeEdge(index)) 
            {
                _localGraph.RemoveEdgeByNonBridgeIndex(index);
                _removedEdges.Add(index);
                return true; 
            }
            //Current value 1 means edge is off
            //case 1 (means mutates to 0 -> add edge):
            else if (curValue == 1) 
            { 
                _removedEdges.Remove(index);
                _localGraph.AddEdgeCopy(_chm.OriginalNonBridgeEdgeFromIndex(index));                
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
        public void ReplaceGraphChromosomeGene(int index, Gene foreignGene)
        {
            if (index < 0 || index >= this.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), 
                    "There is no Gene on index {0} to be replaced.".With(index));
            }

            int foreignGeneValue = (int)foreignGene.Value;
            int localGeneValue = (int)GetGene(index).Value;

            if (foreignGeneValue == localGeneValue)
            {
                return;
            }

            if (TryMutate(index)) ReplaceGene(index, foreignGene);
        }
    }
}