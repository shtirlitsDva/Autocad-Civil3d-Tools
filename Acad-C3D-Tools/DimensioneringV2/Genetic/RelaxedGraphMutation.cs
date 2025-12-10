using GeneticSharp;

using System;

namespace DimensioneringV2.Genetic
{
    internal class RelaxedGraphMutation : MutationBase
    {
        private readonly IRandomization m_rnd;

        public RelaxedGraphMutation()
        {
            m_rnd = RandomizationProvider.Current;
        }

        protected override void PerformMutate(IChromosome chromosome, float probability)
        {
            var relaxedChromosome = chromosome as RelaxedGraphChromosome;

            if (relaxedChromosome == null)
            {
                throw new MutationException(this, "Must be a RelaxedGraphChromosome!");
            }

            if (m_rnd.GetDouble() <= probability)
            {
                var index = m_rnd.GetInt(0, chromosome.Length);
                relaxedChromosome.Mutate(index);
                relaxedChromosome.FlipGene(index);
            }
        }
    }
}
