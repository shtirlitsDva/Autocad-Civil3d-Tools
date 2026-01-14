namespace NorsynHydraulicCalc.Rules
{
    /// <summary>
    /// Context provided by consumers (e.g., DimensioneringV2) for rule evaluation.
    /// NHS defines rules but doesn't know about graph structure - consumers provide context.
    /// Designed for extensibility to support future rule types.
    /// </summary>
    public interface IRuleEvaluationContext
    {
        /// <summary>
        /// The pipe type of the parent FL edge (for Stikledninger).
        /// Null if not applicable or not yet determined.
        /// </summary>
        PipeType? ParentPipeType { get; }

        // Future extensibility:
        // - ParentDn for DN-based rules
        // - DistanceFromRoot for topology-based rules
        // - etc.
    }

    /// <summary>
    /// Default implementation of rule evaluation context.
    /// </summary>
    public class RuleEvaluationContext : IRuleEvaluationContext
    {
        /// <summary>
        /// Gets or sets the parent FL pipe type.
        /// </summary>
        public PipeType? ParentPipeType { get; set; }

        /// <summary>
        /// Creates an empty context.
        /// </summary>
        public RuleEvaluationContext() { }

        /// <summary>
        /// Creates a context with a parent pipe type.
        /// </summary>
        public RuleEvaluationContext(PipeType parentPipeType)
        {
            ParentPipeType = parentPipeType;
        }

        /// <summary>
        /// Creates an empty context (no parent information available).
        /// </summary>
        public static IRuleEvaluationContext Empty => new RuleEvaluationContext();

        /// <summary>
        /// Creates a context with the specified parent pipe type.
        /// </summary>
        public static IRuleEvaluationContext ForParent(PipeType parentPipeType)
            => new RuleEvaluationContext(parentPipeType);
    }
}
