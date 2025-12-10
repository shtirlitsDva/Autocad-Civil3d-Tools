using GeneticSharp;

using System;
using System.Collections.Generic;

namespace DimensioneringV2.Genetic
{
    internal class RelaxedCrossover : CrossoverBase
    {
        private readonly CoherencyManager _chm;

        public RelaxedCrossover(CoherencyManager coherencyManager) : base(2, 2)
        {
            _chm = coherencyManager;
        }

        protected override IList<IChromosome> PerformCross(IList<IChromosome> parents)
        {
            var firstParent = parents[0];
            var secondParent = parents[1];
            var firstChild = firstParent.CreateNew();
            var secondChild = secondParent.CreateNew();

            RelaxedGraphChromosome fc = (RelaxedGraphChromosome)firstChild;
            RelaxedGraphChromosome sc = (RelaxedGraphChromosome)secondChild;
            
            fc.ResetChromosome();
            sc.ResetChromosome();

            var rnd = RandomizationProvider.Current;
            int swapPoint = rnd.GetInt(1, _chm.ChromosomeLength - 1);

            for (int i = 0; i < _chm.ChromosomeLength; i++)
            {
                if (i < swapPoint)
                {
                    fc.ReplaceGraphChromosomeGene(i, firstParent.GetGene(i));
                    sc.ReplaceGraphChromosomeGene(i, secondParent.GetGene(i));
                }
                else
                {
                    fc.ReplaceGraphChromosomeGene(i, secondParent.GetGene(i));
                    sc.ReplaceGraphChromosomeGene(i, firstParent.GetGene(i));
                }
            }

            return new List<IChromosome> { fc, sc };
        }
    }
}
