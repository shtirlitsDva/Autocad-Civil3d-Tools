using GeneticSharp;

using System;

namespace DimensioneringV2.Genetic
{
    /// <summary>
    /// Simple binary chromosome for relaxed GA mode.
    /// Does NOT track graph state - graph is rebuilt at fitness evaluation time.
    /// Can be used with any standard GeneticSharp crossover and mutation operators.
    /// 1 = edge is off, 0 = edge is on
    /// </summary>
    internal class RelaxedGraphChromosome : BinaryChromosomeBase
    {
        private readonly CoherencyManager _chm;

        public CoherencyManager CoherencyManager => _chm;

        public RelaxedGraphChromosome(CoherencyManager coherencyManager) : base(coherencyManager.ChromosomeLength)
        {
            _chm = coherencyManager;

            var random = RandomizationProvider.Current;

            // Thread-safe seeding: only the first chromosome gets the seed
            if (_chm.TryClaimSeed())
            {
                // Initialize from seed - determine which edges are in the seed graph
                var seedEdgeIndices = new System.Collections.Generic.HashSet<int>();
                foreach (var edge in _chm.Seed.Edges)
                {
                    if (edge.NonBridgeChromosomeIndex >= 0)
                        seedEdgeIndices.Add(edge.NonBridgeChromosomeIndex);
                }

                for (int i = 0; i < _chm.ChromosomeLength; i++)
                {
                    // 0 = edge on, 1 = edge off
                    ReplaceGene(i, new Gene(seedEdgeIndices.Contains(i) ? 0 : 1));
                }
            }
            else
            {
                // Random initialization - just flip bits randomly
                for (int i = 0; i < _chm.ChromosomeLength; i++)
                {
                    ReplaceGene(i, new Gene(random.GetDouble() >= 0.5 ? 1 : 0));
                }
            }
        }

        public override IChromosome CreateNew()
        {
            return new RelaxedGraphChromosome(_chm);
        }
    }
}
