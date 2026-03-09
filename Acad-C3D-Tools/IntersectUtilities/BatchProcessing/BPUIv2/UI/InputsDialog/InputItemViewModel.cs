using CommunityToolkit.Mvvm.ComponentModel;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.InputsDialog;

public partial class InputItemViewModel : ObservableObject
{
    public string StepId { get; init; } = string.Empty;
    public string StepDisplayName { get; init; } = string.Empty;
    public string ParamName { get; init; } = string.Empty;
    public string ParamDisplayName { get; init; } = string.Empty;
    public ParameterType ParamType { get; init; }

    public bool IsBound { get; init; }
    public string BindingDisplayText { get; init; } = string.Empty;

    [ObservableProperty] private string stringValue = string.Empty;
    [ObservableProperty] private bool boolValue;
    [ObservableProperty] private string selectedEnumValue = string.Empty;
    public string[] EnumChoices { get; init; } = Array.Empty<string>();

    public bool IsEditable => !IsBound && ParamType is not ParameterType.DataReferencesOptions
        and not ParameterType.Counter and not ParameterType.FilterSet;
    public bool IsStringInput => IsEditable && ParamType is ParameterType.String or ParameterType.Int or ParameterType.Double;
    public bool IsBoolInput => IsEditable && ParamType == ParameterType.Bool;
    public bool IsEnumInput => IsEditable && ParamType == ParameterType.EnumChoice;
    public bool IsSpecialType => ParamType is ParameterType.DataReferencesOptions or ParameterType.Counter or ParameterType.FilterSet;
    public bool NeedsInput => IsEditable && string.IsNullOrEmpty(StringValue) && !IsBound;
}
