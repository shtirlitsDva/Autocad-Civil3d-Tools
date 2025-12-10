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
    /// Strict graph chromosome that validates every mutation to ensure terminal connectivity.
    /// 1 = edge is off, 0 = edge is on
    /// </summary>
    internal class StrictGraphChromosome : BinaryChromosomeBase
    {
        private UndirectedGraph<BFNode, BFEdge> _localGraph;
        private readonly HashSet<int> _removedEdges = new HashSet<int>();
        private Dictionary<int, BFEdge> _edgeByIndex = new();
        private CoherencyManager _chm;

        public UndirectedGraph<BFNode, BFEdge> LocalGraph { get => _localGraph; set => _localGraph = value; }
        public HashSet<int> RemovedEdges => _removedEdges;
        public CoherencyManager CoherencyManager => _chm;

        /// <summary>
        /// Fast O(1) edge lookup by NonBridgeChromosomeIndex.
        /// Returns null if edge not in current local graph.
        /// </summary>
        public BFEdge? GetEdgeByIndex(int index)
        {
            return _edgeByIndex.TryGetValue(index, out var edge) ? edge : null;
        }

        /// <summary>
        /// Rebuilds the edge index dictionary from current local graph state.
        /// Call after bulk graph modifications.
        /// </summary>
        private void RebuildEdgeIndex()
        {
            _edgeByIndex.Clear();
            foreach (var edge in _localGraph.Edges)
            {
                if (edge.NonBridgeChromosomeIndex >= 0)
                    _edgeByIndex[edge.NonBridgeChromosomeIndex] = edge;
            }
        }

        public StrictGraphChromosome(CoherencyManager coherencyManager) : base(coherencyManager.ChromosomeLength)
        {
            _chm = coherencyManager;
            _localGraph = _chm.OriginalGraph.CopyWithNewEdges();
            RebuildEdgeIndex();

            var random = RandomizationProvider.Current;

            // Thread-safe seeding: only the first chromosome gets the seed
            if (_chm.TryClaimSeed())
            {
                _localGraph = _chm.Seed.CopyWithNewEdges();
                RebuildEdgeIndex();

                var set = _localGraph.Edges
                    .Select(x => x.NonBridgeChromosomeIndex)
                    .ToHashSet();

                for (int i = 0; i < _chm.ChromosomeLength; i++)
                {
                    if (set.Contains(i))
                    {
                        ReplaceGene(i, new Gene(0));
                    }
                    else
                    {
                        ReplaceGene(i, new Gene(1));
                        RemovedEdges.Add(i);
                    }
                }

                if (!_localGraph.AreTerminalNodesConnected(_chm.RootNode, _chm.Terminals))
                {
                    throw new Exception("Seeds' terminals are not connected!");
                }
            }
            else
            {
                do
                {
                    // Fisher-Yates shuffle: O(n) instead of O(n log n) OrderBy
                    var randomizedIndici = new int[_chm.ChromosomeLength];
                    for (int i = 0; i < randomizedIndici.Length; i++)
                        randomizedIndici[i] = i;
                    for (int i = randomizedIndici.Length - 1; i > 0; i--)
                    {
                        int j = random.GetInt(0, i + 1);
                        (randomizedIndici[i], randomizedIndici[j]) = (randomizedIndici[j], randomizedIndici[i]);
                    }

                    ResetChromosome();

                    for (int i = 0; i < randomizedIndici.Length; i++)
                    {
                        int rIdx = randomizedIndici[i];

                        // Determine if the edge should be removed
                        bool removeEdge = random.GetDouble() >= 0.5;
                        var edge = GetEdgeByIndex(rIdx); // O(1) lookup instead of O(E)

                        if (edge != null && removeEdge && !_localGraph.IsBridgeEdge(edge))
                        {
                            _localGraph.RemoveEdge(edge);
                            _edgeByIndex.Remove(rIdx); // Keep index in sync
                            _removedEdges.Add(rIdx);
                            ReplaceGene(rIdx, new Gene(1));
                        }
                        else
                        {
                            ReplaceGene(rIdx, new Gene(0));
                        }
                    }
                }
                while (!_localGraph.AreTerminalNodesConnected(_chm.RootNode, _chm.Terminals));
            }
        }

        public override IChromosome CreateNew()
        {
            return new StrictGraphChromosome(_chm);
        }

        public void ResetChromosome()
        {
            _localGraph = _chm.OriginalGraph.CopyWithNewEdges();
            RebuildEdgeIndex();
            _removedEdges.Clear();
            for (int i = 0; i < Length; i++)
            {
                ReplaceGene(i, new Gene(0));
            }
        }

        //public BitArray GetBitArray()
        //{
        //    var bitArray = new BitArray(Length);
        //    for (int i = 0; i < Length; i++)
        //    {
        //        bitArray[i] = (int)GetGene(i).Value == 1;
        //    }
        //    return bitArray;
        //}

        public bool TryMutate(int index)
        {
            int curValue = (int)GetGene(index).Value;

            //current value 0 means edge is on
            //case 0 (means mutates to 1 -> remove edge):
            if (curValue == 0)
            {
                var edge = GetEdgeByIndex(index); // O(1) lookup
                if (edge == null) return false; // Edge not in graph
                
                if (_localGraph.IsBridgeEdge(edge)) return false; // Can't remove bridge
                
                _localGraph.RemoveEdge(edge);
                _edgeByIndex.Remove(index); // Keep index in sync
                _removedEdges.Add(index);
                return true;
            }
            //Current value 1 means edge is off
            //case 1 (means mutates to 0 -> add edge):
            else if (curValue == 1)
            {
                _removedEdges.Remove(index);
                var originalEdge = _chm.OriginalNonBridgeEdgeFromIndex(index);
                var newEdge = new BFEdge(originalEdge.Source, originalEdge.Target, originalEdge);
                newEdge.NonBridgeChromosomeIndex = originalEdge.NonBridgeChromosomeIndex;
                _localGraph.AddVerticesAndEdge(newEdge);
                _edgeByIndex[index] = newEdge; // Keep index in sync
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

        internal void UpdateChromosome(UndirectedGraph<BFNode, BFEdge> graph)
        {
            _localGraph = graph;
            RebuildEdgeIndex();

            HashSet<int> newTurnedOnEdges = 
                graph.Edges.Select(x => x.NonBridgeChromosomeIndex)
                .ToHashSet();

            _removedEdges.Clear();

            for (int i = 0; i < Length; i++)
            {
                if (newTurnedOnEdges.Contains(i))
                {
                    ReplaceGene(i, new Gene(0));
                }
                else
                {
                    ReplaceGene(i, new Gene(1));
                    _removedEdges.Add(i);
                }
            }
        }
    }
}