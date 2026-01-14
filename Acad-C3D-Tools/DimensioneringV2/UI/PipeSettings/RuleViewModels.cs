using CommunityToolkit.Mvvm.ComponentModel;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Rules;

namespace DimensioneringV2.UI.PipeSettings
{
    /// <summary>
    /// Base class for rule view models that wrap NHS rule types.
    /// </summary>
    public abstract partial class RuleViewModelBase : ObservableObject
    {
        /// <summary>
        /// Display name for the rule type.
        /// </summary>
        public abstract string RuleTypeName { get; }

        /// <summary>
        /// Description of what this rule does.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Creates the underlying NHS rule from this view model.
        /// </summary>
        public abstract IPipeRule ToRule();

        /// <summary>
        /// Creates a view model from an NHS rule.
        /// </summary>
        public static RuleViewModelBase FromRule(IPipeRule rule)
        {
            return rule switch
            {
                ParentPipeRule ppr => new ParentPipeRuleViewModel(ppr.ParentPipeType),
                _ => throw new System.ArgumentException($"Unknown rule type: {rule.GetType()}")
            };
        }
    }

    /// <summary>
    /// View model for ParentPipeRule - wraps the NHS type for UI binding.
    /// </summary>
    public partial class ParentPipeRuleViewModel : RuleViewModelBase
    {
        [ObservableProperty]
        private PipeType parentPipeType;

        public override string RuleTypeName => "Forældrerør";

        public override string Description => $"Bruges når forældrerør er {ParentPipeType}";

        public ParentPipeRuleViewModel()
        {
        }

        public ParentPipeRuleViewModel(PipeType parentPipeType)
        {
            ParentPipeType = parentPipeType;
        }

        public override IPipeRule ToRule()
        {
            return new ParentPipeRule(ParentPipeType);
        }

        partial void OnParentPipeTypeChanged(PipeType value)
        {
            OnPropertyChanged(nameof(Description));
        }
    }
}
