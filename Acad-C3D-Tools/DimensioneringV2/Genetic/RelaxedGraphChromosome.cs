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
    internal class RelaxedGraphChromosome : GraphChromosomeBase
    {
        public RelaxedGraphChromosome(CoherencyManager coherencyManager) : base(coherencyManager)
        {
            var random = RandomizationProvider.Current;

            // Thread-safe seeding: only the first chromosome gets the seed
            if (CoherencyManager.TryClaimSeed())
            {
                // Initialize from seed - determine which edges are in the seed graph
                var seedEdgeIndices = new System.Collections.Generic.HashSet<int>();
                foreach (var edge in CoherencyManager.Seed.Edges)
                {
                    if (edge.NonBridgeChromosomeIndex >= 0)
                        seedEdgeIndices.Add(edge.NonBridgeChromosomeIndex);
                }

                for (int i = 0; i < CoherencyManager.ChromosomeLength; i++)
                {
                    // 0 = edge on, 1 = edge off
                    ReplaceGene(i, new Gene(seedEdgeIndices.Contains(i) ? 0 : 1));
                }
            }
            else
            {
                // Random initialization - just flip bits randomly
                for (int i = 0; i < CoherencyManager.ChromosomeLength; i++)
                {
                    ReplaceGene(i, new Gene(random.GetDouble() >= 0.5 ? 1 : 0));
                }
            }
        }

        public override IChromosome CreateNew()
        {
            return new RelaxedGraphChromosome(CoherencyManager);
        }
    }
}
