using GeneticSharp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Genetic
{
    internal class GraphMutation : MutationBase
    {
        protected override void PerformMutate(IChromosome chromosome, float probability)
        {
            if (chromosome is not GraphChromosome graphChromosome)
                throw new ArgumentException("Invalid chromosome type.");

            var random = RandomizationProvider.Current;
            var geneIndex = random.GetInt(0, graphChromosome.Length);
            var currentState = (bool)graphChromosome.GetGene(geneIndex).Value;

            // Try to toggle the state, ensuring validity
            //if (graphChromosome.IsValidMutation(geneIndex, !currentState))
            //{
            //    graphChromosome.ReplaceGene(geneIndex, new Gene(!currentState));
            //}
        }
    }
}
