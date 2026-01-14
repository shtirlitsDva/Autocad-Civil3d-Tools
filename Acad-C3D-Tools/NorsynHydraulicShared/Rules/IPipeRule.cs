using System.Text.Json.Serialization;

namespace NorsynHydraulicCalc.Rules
{
    /// <summary>
    /// Enum defining the types of rules that can be applied to pipe priorities.
    /// </summary>
    public enum PipeRuleType
    {
        /// <summary>
        /// Parent pipe rule - SL pipe type depends on parent FL pipe type.
        /// </summary>
        ParentPipe
    }

    /// <summary>
    /// Base interface for rules that control when a pipe priority is applicable.
    /// Used primarily for Stikledninger where pipe selection may depend on context.
    /// NHS defines rules but doesn't know about graph structure - consumers provide context.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ParentPipeRule), "ParentPipe")]
    public interface IPipeRule
    {
        /// <summary>
        /// Gets the type of this rule.
        /// </summary>
        PipeRuleType RuleType { get; }

        /// <summary>
        /// Evaluates the rule against the provided context.
        /// </summary>
        /// <param name="context">Context provided by consumer containing relevant data for evaluation.</param>
        /// <returns>True if the rule matches (priority should be used), false otherwise.</returns>
        bool Evaluate(IRuleEvaluationContext context);

        /// <summary>
        /// Creates a deep copy of this rule.
        /// </summary>
        IPipeRule Clone();
    }
}
