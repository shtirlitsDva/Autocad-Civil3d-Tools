using GeneticSharp;

namespace DimensioneringV2.Genetic
{
    internal abstract class GraphChromosomeBase : BinaryChromosomeBase
    {
        public CoherencyManager CoherencyManager { get; }

        protected GraphChromosomeBase(CoherencyManager coherencyManager)
            : base(coherencyManager.ChromosomeLength)
        {
            CoherencyManager = coherencyManager;
        }
    }
}
