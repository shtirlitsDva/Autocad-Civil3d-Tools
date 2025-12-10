using GeneticSharp;

using System;

namespace DimensioneringV2.Genetic.Operators
{
    public record ParameterDescriptor(
        string Name,
        string DisplayName,
        Type Type,
        object DefaultValue,
        object? Min = null,
        object? Max = null);

    public record OperatorDescriptor(
        string DisplayName,
        Type OperatorType,
        ParameterDescriptor[] Parameters);

    public static class OperatorRegistry
    {
        public static readonly OperatorDescriptor[] SelectionOperators = new[]
        {
            new OperatorDescriptor("Elite", typeof(EliteSelection), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Roulette Wheel", typeof(RouletteWheelSelection), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Stochastic Universal Sampling", typeof(StochasticUniversalSamplingSelection), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Tournament", typeof(TournamentSelection), new[] { new ParameterDescriptor("Size", "Tournament Size", typeof(int), 3, 2, 10) }),
            new OperatorDescriptor("Truncation", typeof(TruncationSelection), Array.Empty<ParameterDescriptor>())
        };

        public static readonly OperatorDescriptor[] CrossoverOperators = new[]
        {
            new OperatorDescriptor("One Point", typeof(OnePointCrossover), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Two Point", typeof(TwoPointCrossover), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Uniform", typeof(UniformCrossover), new[] { new ParameterDescriptor("MixProbability", "Mix Probability", typeof(float), 0.5f, 0.0f, 1.0f) }),
            new OperatorDescriptor("Three Parent", typeof(ThreeParentCrossover), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Strict Unique", typeof(StrictUniqueCrossover), new[] { new ParameterDescriptor("MixProbability", "Mix Probability", typeof(float), 0.5f, 0.0f, 1.0f) }),
            new OperatorDescriptor("Relaxed", typeof(RelaxedCrossover), Array.Empty<ParameterDescriptor>())
        };

        public static readonly OperatorDescriptor[] MutationOperators = new[]
        {
            new OperatorDescriptor("Flip Bit", typeof(FlipBitMutation), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Strict Graph", typeof(StrictGraphMutation), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Relaxed Graph", typeof(RelaxedGraphMutation), Array.Empty<ParameterDescriptor>())
        };

        public static readonly OperatorDescriptor[] ReinsertionOperators = new[]
        {
            new OperatorDescriptor("Elitist", typeof(ElitistReinsertion), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Fitness Based", typeof(FitnessBasedReinsertion), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Pure", typeof(PureReinsertion), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Uniform", typeof(UniformReinsertion), Array.Empty<ParameterDescriptor>())
        };

        public static readonly OperatorDescriptor[] TerminationOperators = new[]
        {
            new OperatorDescriptor("Generation Number", typeof(GenerationNumberTermination), new[] { new ParameterDescriptor("ExpectedGenerationNumber", "Max Generations", typeof(int), 100, 1, 10000) }),
            new OperatorDescriptor("Time Evolving", typeof(TimeEvolvingTermination), new[] { new ParameterDescriptor("MaxTime", "Max Time (seconds)", typeof(int), 60, 1, 3600) }),
            new OperatorDescriptor("Fitness Stagnation", typeof(FitnessStagnationTermination), new[] { new ParameterDescriptor("ExpectedStagnantGenerationsNumber", "Stagnant Generations", typeof(int), 100, 1, 1000) }),
            new OperatorDescriptor("Fitness Threshold", typeof(FitnessThresholdTermination), new[] { new ParameterDescriptor("ExpectedFitness", "Target Fitness", typeof(double), 0.95, 0.0, 1.0) })
        };

        public static readonly OperatorDescriptor[] TaskExecutorOperators = new[]
        {
            new OperatorDescriptor("TPL", typeof(TplTaskExecutor), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Parallel", typeof(ParallelTaskExecutor), new[] { new ParameterDescriptor("MinThreads", "Min Threads", typeof(int), 1, 1, Environment.ProcessorCount), new ParameterDescriptor("MaxThreads", "Max Threads", typeof(int), Environment.ProcessorCount, 1, Environment.ProcessorCount * 2) }),
            new OperatorDescriptor("Linear", typeof(LinearTaskExecutor), Array.Empty<ParameterDescriptor>())
        };

        public static readonly OperatorDescriptor[] ChromosomeTypes = new[]
        {
            new OperatorDescriptor("Strict", typeof(StrictGraphChromosome), Array.Empty<ParameterDescriptor>()),
            new OperatorDescriptor("Relaxed", typeof(RelaxedGraphChromosome), Array.Empty<ParameterDescriptor>())
        };
    }
}
