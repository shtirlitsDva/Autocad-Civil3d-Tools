using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphFeatures;

using GeneticSharp;

using Mapsui.Utilities;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Genetic
{
    /// <summary>
    /// Relaxed graph chromosome that allows free bit flipping without connectivity validation.
    /// May produce disconnected trees - relies on fitness penalty for invalid solutions.
    /// 1 = edge is off, 0 = edge is on
    /// </summary>
    internal class RelaxedGraphChromosome : BinaryChromosomeBase
    {
        private UndirectedGraph<BFNode, BFEdge> _localGraph;
        private readonly HashSet<int> _removedEdges = new HashSet<int>();
        private Dictionary<int, BFEdge> _edgeByIndex = new();
        private CoherencyManager _chm;

        public UndirectedGraph<BFNode, BFEdge> LocalGraph { get => _localGraph; set => _localGraph = value; }
        public HashSet<int> RemovedEdges => _removedEdges;
        public CoherencyManager CoherencyManager => _chm;

        public BFEdge? GetEdgeByIndex(int index)
        {
            return _edgeByIndex.TryGetValue(index, out var edge) ? edge : null;
        }

        private void RebuildEdgeIndex()
        {
            _edgeByIndex.Clear();
            foreach (var edge in _localGraph.Edges)
            {
                if (edge.NonBridgeChromosomeIndex >= 0)
                    _edgeByIndex[edge.NonBridgeChromosomeIndex] = edge;
            }
        }

        public RelaxedGraphChromosome(CoherencyManager coherencyManager) : base(coherencyManager.ChromosomeLength)
        {
            _chm = coherencyManager;
            _localGraph = _chm.OriginalGraph.CopyWithNewEdges();
            RebuildEdgeIndex();

            var random = RandomizationProvider.Current;

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
            }
            else
            {
                for (int i = 0; i < _chm.ChromosomeLength; i++)
                {
                    bool removeEdge = random.GetDouble() >= 0.5;
                    
                    if (removeEdge)
                    {
                        var edge = GetEdgeByIndex(i);
                        if (edge != null)
                        {
                            _localGraph.RemoveEdge(edge);
                            _edgeByIndex.Remove(i);
                        }
                        _removedEdges.Add(i);
                        ReplaceGene(i, new Gene(1));
                    }
                    else
                    {
                        ReplaceGene(i, new Gene(0));
                    }
                }
            }
        }

        public override IChromosome CreateNew()
        {
            return new RelaxedGraphChromosome(_chm);
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

        public bool Mutate(int index)
        {
            int curValue = (int)GetGene(index).Value;

            if (curValue == 0)
            {
                var edge = GetEdgeByIndex(index);
                if (edge != null)
                {
                    _localGraph.RemoveEdge(edge);
                    _edgeByIndex.Remove(index);
                }
                _removedEdges.Add(index);
                return true;
            }
            else if (curValue == 1)
            {
                _removedEdges.Remove(index);
                var originalEdge = _chm.OriginalNonBridgeEdgeFromIndex(index);
                var newEdge = new BFEdge(originalEdge.Source, originalEdge.Target, originalEdge);
                newEdge.NonBridgeChromosomeIndex = originalEdge.NonBridgeChromosomeIndex;
                _localGraph.AddVerticesAndEdge(newEdge);
                _edgeByIndex[index] = newEdge;
                return true;
            }
            
            return false;
        }

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

            Mutate(index);
            ReplaceGene(index, foreignGene);
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
