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

        ConcurrentHashSet<BitArray> _uniqueBitRepresentations;

        public UniqueCrossover(ConcurrentHashSet<BitArray> uniqueBitRepresentations) : base(2,2)
        {
            _uniqueBitRepresentations = uniqueBitRepresentations;
        }

        protected override IList<IChromosome> PerformCross(IList<IChromosome> parents)
        {
            
        }
    }
}
