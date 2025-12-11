using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Genetic
{
    /// <summary>
    /// Enumeration of available chromosome types for the GA.
    /// </summary>
    public enum ChromosomeType
    {
        /// <summary>
        /// Validates every mutation, rejects changes that break terminal connectivity.
        /// Conservative exploration, guaranteed valid solutions.
        /// </summary>
        Strict,

        /// <summary>
        /// Allows free bit flipping, may produce disconnected trees.
        /// Aggressive exploration, relies on fitness penalty.
        /// </summary>
        Relaxed
    }

    /// <summary>
    /// Enumeration of available selection operators.
    /// </summary>
    public enum SelectionType
    {
        Elite,
        RouletteWheel,
        StochasticUniversalSampling,
        Tournament,
        Truncation
    }

    /// <summary>
    /// Enumeration of available crossover operators.
    /// Standard GeneticSharp operators work with any chromosome type.
    /// StrictUnique is domain-aware and maintains graph connectivity.
    /// </summary>
    public enum CrossoverType
    {
        OnePoint,
        TwoPoint,
        Uniform,
        ThreeParent,
        StrictUnique
    }

    /// <summary>
    /// Enumeration of available mutation operators.
    /// FlipBit is standard GeneticSharp operator (works with any chromosome).
    /// StrictGraph is domain-aware and validates graph connectivity.
    /// </summary>
    public enum MutationType
    {
        FlipBit,
        StrictGraph
    }

    /// <summary>
    /// Enumeration of available reinsertion operators.
    /// </summary>
    public enum ReinsertionType
    {
        Elitist,
        FitnessBased,
        Pure,
        Uniform
    }

    /// <summary>
    /// Enumeration of available termination conditions.
    /// </summary>
    public enum TerminationType
    {
        GenerationNumber,
        TimeEvolving,
        FitnessStagnation,
        FitnessThreshold
    }

    /// <summary>
    /// Enumeration of available task executors.
    /// </summary>
    public enum TaskExecutorType
    {
        Tpl,
        Parallel,
        Linear
    }

    /// <summary>
    /// Settings model for Genetic Algorithm configuration.
    /// Uses CommunityToolkit.Mvvm for observable properties.
    /// </summary>
    public partial class GASettings : ObservableObject
    {
        #region Population Settings
        [ObservableProperty]
        private int populationMinSize = 50;

        [ObservableProperty]
        private int populationMaxSize = 100;
        #endregion

        #region Chromosome Settings
        [ObservableProperty]
        private ChromosomeType chromosomeType = ChromosomeType.Relaxed;

        /// <summary>
        /// When enabled, disconnected graphs receive graduated penalties based on
        /// number of disconnected terminals instead of -double.MaxValue.
        /// Only applicable for Relaxed chromosome type.
        /// </summary>
        [ObservableProperty]
        private bool useGraduatedPenalty = true;
        #endregion

        #region Selection Settings
        [ObservableProperty]
        private SelectionType selectionType = SelectionType.Elite;

        [ObservableProperty]
        private int tournamentSize = 3;
        #endregion

        #region Crossover Settings
        [ObservableProperty]
        private double crossoverProbability = 0.7;

        [ObservableProperty]
        private CrossoverType crossoverType = CrossoverType.Uniform;

        [ObservableProperty]
        private float uniformCrossoverMixProbability = 0.7f;

        [ObservableProperty]
        private float strictUniqueCrossoverMixProbability = 0.5f;
        #endregion

        #region Mutation Settings
        [ObservableProperty]
        private float mutationProbability = 0.9f;

        [ObservableProperty]
        private MutationType mutationType = MutationType.FlipBit;
        #endregion

        #region Reinsertion Settings
        [ObservableProperty]
        private ReinsertionType reinsertionType = ReinsertionType.Elitist;
        #endregion

        #region Termination Settings
        [ObservableProperty]
        private TerminationType terminationType = TerminationType.FitnessStagnation;

        [ObservableProperty]
        private int generationNumberTerminationCount = 100;

        [ObservableProperty]
        private int fitnessStagnationTerminationCount = 1000;

        [ObservableProperty]
        private double fitnessThresholdTerminationValue = 0.95;

        [ObservableProperty]
        private int timeEvolvingTerminationSeconds = 60;
        #endregion

        #region Task Executor Settings
        [ObservableProperty]
        private TaskExecutorType taskExecutorType = TaskExecutorType.Tpl;

        [ObservableProperty]
        private int parallelTaskExecutorMinThreads = 1;

        [ObservableProperty]
        private int parallelTaskExecutorMaxThreads = Environment.ProcessorCount;
        #endregion

        /// <summary>
        /// Copies all settings from another GASettings instance.
        /// </summary>
        internal void CopyFrom(GASettings src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            foreach (var p in typeof(GASettings)
                              .GetProperties(System.Reflection.BindingFlags.Instance |
                                             System.Reflection.BindingFlags.Public)
                              .Where(pr => pr.CanRead && pr.CanWrite && pr.GetIndexParameters().Length == 0))
            {
                p.SetValue(this, p.GetValue(src));
            }
        }
    }
}
