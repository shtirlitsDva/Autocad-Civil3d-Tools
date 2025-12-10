using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.ResultCache;
using DimensioneringV2.Services;

using GeneticSharp;

using QuikGraph;

using System;
using System.Collections.Generic;

namespace DimensioneringV2.Genetic
{
    internal class GraphFitness : IFitness
    {
        private readonly List<SumProperty<BFEdge>> _props;
        private readonly CoherencyManager _chm;
        private readonly HydraulicCalculationCache<BFEdge> _cache;
        private readonly bool _useGraduatedPenalty;

        public GraphFitness(
            CoherencyManager coherencyManager,
            List<SumProperty<BFEdge>> props,
            HydraulicCalculationCache<BFEdge> cache)
        {
            _props = props;
            _chm = coherencyManager;
            _cache = cache;
            _useGraduatedPenalty = GASettingsService.Instance.Settings.UseGraduatedPenalty;
        }

        public double Evaluate(IChromosome chromosome)
        {
            // Always rebuild graph from genes using interface methods.
            // This allows any chromosome type to work with any crossover/mutation operator.
            var localGraph = _chm.RebuildGraphFromChromosome(chromosome);
            
            // Check terminal connectivity - apply penalty if disconnected
            if (!localGraph.AreTerminalNodesConnected(_chm.RootNode, _chm.Terminals))
            {
                if (!_useGraduatedPenalty)
                {
                    return -double.MaxValue;
                }

                // Graduated penalty: count how many terminals are connected
                int connectedCount = CountConnectedTerminals(localGraph);
                int disconnectedCount = _chm.TotalTerminalCount - connectedCount;

                // If no terminals are reachable at all, apply maximum penalty
                if (disconnectedCount >= _chm.TotalTerminalCount)
                {
                    return -double.MaxValue;
                }

                // Graduated penalty: penalty proportional to disconnected terminals
                // More disconnected = worse fitness (more negative)
                return -_chm.GraduatedPenaltyUpperBound * disconnectedCount;
            }

            double result = HCS_SGC_CalculateSumsAndCost.CalculateSumsAndCost(
                localGraph, _chm.OriginalGraph, _props, _chm.MetaGraph, _cache);

            return -result;
        }

        /// <summary>
        /// Counts the number of terminal nodes reachable from the root node using BFS.
        /// </summary>
        private int CountConnectedTerminals(UndirectedGraph<BFNode, BFEdge> graph)
        {
            var visited = new HashSet<BFNode>();
            var stack = new Stack<BFNode>();
            stack.Push(_chm.RootNode);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (!visited.Add(node)) continue;

                foreach (var neighbor in graph.AdjacentVertices(node))
                {
                    if (!visited.Contains(neighbor))
                        stack.Push(neighbor);
                }
            }

            // Count how many terminals are in the visited set
            int count = 0;
            foreach (var terminal in _chm.Terminals)
            {
                if (visited.Contains(terminal))
                    count++;
            }
            return count;
        }
    }
}
