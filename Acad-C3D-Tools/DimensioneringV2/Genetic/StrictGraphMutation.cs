using GeneticSharp;

using Mapsui.Utilities;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Genetic
{
    /// <summary>
    /// Strict mutation operator that validates mutations don't break terminal connectivity.
    /// Works only with StrictGraphChromosome.
    /// </summary>
    internal class StrictGraphMutation : MutationBase
    {
        public StrictGraphMutation()
        {
            m_rnd = RandomizationProvider.Current;
        }

        #region Fields
        private readonly IRandomization m_rnd;
        #endregion

        protected override void PerformMutate(IChromosome chromosome, float probability)
        {
            var binaryChromosome = chromosome as StrictGraphChromosome;

            if (binaryChromosome == null)
            {
                throw new MutationException(this, "Must be a StrictGraphChromosome!");
            }

            if (m_rnd.GetDouble() <= probability)
            {
                var index = m_rnd.GetInt(0, chromosome.Length);

                if (binaryChromosome.TryMutate(index))
                {
                    binaryChromosome.FlipGene(index);
                }
            }
        }
    }
}
