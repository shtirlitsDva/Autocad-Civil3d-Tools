namespace NorsynHydraulicCalc.Rules
{
    /// <summary>
    /// Rule that matches when the parent FL pipe is of a specific type.
    /// Used for Stikledninger to determine pipe type based on the connected Fordelingsledning.
    /// </summary>
    public class ParentPipeRule : IPipeRule
    {
        /// <summary>
        /// Gets the rule type.
        /// </summary>
        public PipeRuleType RuleType => PipeRuleType.ParentPipe;

        /// <summary>
        /// The parent FL pipe type that must match for this rule to apply.
        /// </summary>
        public PipeType ParentPipeType { get; set; }

        /// <summary>
        /// Parameterless constructor for serialization.
        /// </summary>
        public ParentPipeRule() { }

        /// <summary>
        /// Creates a new parent pipe rule for the specified pipe type.
        /// </summary>
        /// <param name="parentPipeType">The FL pipe type that must match.</param>
        public ParentPipeRule(PipeType parentPipeType)
        {
            ParentPipeType = parentPipeType;
        }

        /// <summary>
        /// Evaluates the rule against the provided context.
        /// Returns true if context contains a parent pipe type that matches this rule's expected type.
        /// </summary>
        /// <param name="context">Context containing parent pipe information.</param>
        /// <returns>True if the parent pipe type matches.</returns>
        public bool Evaluate(IRuleEvaluationContext context)
        {
            if (context.ParentPipeType == null)
                return false;

            return ParentPipeType == context.ParentPipeType.Value;
        }

        /// <summary>
        /// Checks if this rule matches the given parent pipe type.
        /// </summary>
        /// <param name="actualParentPipeType">The actual parent FL pipe type.</param>
        /// <returns>True if the rule matches.</returns>
        public bool Matches(PipeType actualParentPipeType)
        {
            return ParentPipeType == actualParentPipeType;
        }

        /// <summary>
        /// Creates a deep copy of this rule.
        /// </summary>
        public IPipeRule Clone()
        {
            return new ParentPipeRule(ParentPipeType);
        }

        public override string ToString() => $"Forældrerør: {ParentPipeType}";
    }
}
