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
    internal class UniqueCrossover : CrossoverBase
    {

        private readonly CoherencyManager _chm;
        private readonly float MixProbability;

        public UniqueCrossover(CoherencyManager coherencyManager, float mixProbability) : base(2,2)
        {
            _chm = coherencyManager;
            this.MixProbability = mixProbability;
        }

        protected override IList<IChromosome> PerformCross(IList<IChromosome> parents)
        {
            var firstParent = parents[0];
            var secondParent = parents[1];
            var firstChild = firstParent.CreateNew();
            var secondChild = secondParent.CreateNew();

            GraphChromosome fc = (GraphChromosome)firstChild;
            GraphChromosome sc = (GraphChromosome)secondChild;
            //Replace graph chromosome works only when chromosoe is reset
            fc.ResetChromosome();
            sc.ResetChromosome();

            var randomizedIndici =
                Enumerable.Range(0, _chm.ChromosomeLength)
                .OrderBy(x => RandomizationProvider.Current.GetDouble()).ToArray();

            for (int i = 0; i < randomizedIndici.Length; i++)
            {
                int rIdx = randomizedIndici[i];

                if (RandomizationProvider.Current.GetDouble() < this.MixProbability)
                {
                    fc.ReplaceGraphChromosomeGene(rIdx, firstParent.GetGene(rIdx));
                    sc.ReplaceGraphChromosomeGene(rIdx, secondParent.GetGene(rIdx));
                }
                else
                {
                    fc.ReplaceGraphChromosomeGene(rIdx, secondParent.GetGene(rIdx));
                    sc.ReplaceGraphChromosomeGene(rIdx, firstParent.GetGene(rIdx));
                }
            }

            return new List<IChromosome> { fc, sc };
        }
    }
}