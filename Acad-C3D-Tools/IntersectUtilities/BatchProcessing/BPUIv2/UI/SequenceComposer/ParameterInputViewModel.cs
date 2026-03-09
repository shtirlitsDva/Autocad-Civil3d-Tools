using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.BatchProcessing.BPUIv2.UI.FilterEditor;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.SequenceComposer;

public partial class ParameterInputViewModel : ObservableObject
{
    public ParameterInputViewModel()
    {
        availableOutputs.CollectionChanged += OnAvailableOutputsCollectionChanged;
    }

    private void OnAvailableOutputsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAvailableOutputs));
    }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private ParameterType parameterType;

    [ObservableProperty]
    private string stringValue = string.Empty;

    [ObservableProperty]
    private bool boolValue;

    [ObservableProperty]
    private string selectedEnumValue = string.Empty;

    [ObservableProperty]
    private string[] enumChoices = Array.Empty<string>();

    [ObservableProperty]
    private bool supportsSampling;

    [ObservableProperty]
    private EntityFilterSet? filterSetValue;

    [ObservableProperty]
    private OutputBinding? binding;

    [ObservableProperty]
    private ObservableCollection<AvailableOutput> availableOutputs = new();

    [ObservableProperty]
    private string bindingDisplayText = string.Empty;

    public bool IsBound => Binding != null;
    public bool IsBoundInput => IsBound;
    public bool HasAvailableOutputs => !IsBound && AvailableOutputs.Count > 0;

    public Action<ParameterInputViewModel>? RequestSample { get; set; }

    [RelayCommand]
    private void Sample() => RequestSample?.Invoke(this);

    [RelayCommand]
    private void ConfigureSpecial()
    {
        try
        {
            if (ParameterType == ParameterType.FilterSet)
            {
                var dialog = new FilterEditorDialog(FilterSetValue);
                if (dialog.ShowDialog() == true)
                {
                    FilterSetValue = dialog.ResultFilterSet;
                }
            }
        }
        catch (Exception ex)
        {
            prdDbg($"BPUIv2: ConfigureSpecial error: {ex}");
            MessageBox.Show(
                $"Failed to open editor:\n{ex}",
                "BPv2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public bool IsStringInput => !IsBound && ParameterType is ParameterType.String
        or ParameterType.Int or ParameterType.Double;

    public bool IsBoolInput => !IsBound && ParameterType == ParameterType.Bool;

    public bool IsEnumInput => !IsBound && ParameterType == ParameterType.EnumChoice;

    public bool IsSpecialInput => !IsBound && ParameterType is ParameterType.DataReferencesOptions
        or ParameterType.Counter or ParameterType.FilterSet;

    partial void OnAvailableOutputsChanged(
        ObservableCollection<AvailableOutput> oldValue,
        ObservableCollection<AvailableOutput> newValue)
    {
        if (oldValue != null)
            oldValue.CollectionChanged -= OnAvailableOutputsCollectionChanged;
        if (newValue != null)
            newValue.CollectionChanged += OnAvailableOutputsCollectionChanged;
        OnPropertyChanged(nameof(HasAvailableOutputs));
    }

    partial void OnParameterTypeChanged(ParameterType value)
    {
        RaiseInputTypeProperties();
    }

    partial void OnBindingChanged(OutputBinding? value)
    {
        if (value == null)
            BindingDisplayText = string.Empty;
        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(IsBoundInput));
        OnPropertyChanged(nameof(HasAvailableOutputs));
        RaiseInputTypeProperties();
    }

    private void RaiseInputTypeProperties()
    {
        OnPropertyChanged(nameof(IsStringInput));
        OnPropertyChanged(nameof(IsBoolInput));
        OnPropertyChanged(nameof(IsEnumInput));
        OnPropertyChanged(nameof(IsSpecialInput));
    }

    [RelayCommand]
    private void BindTo(AvailableOutput output)
    {
        Binding = new OutputBinding
        {
            SourceStepId = output.StepId,
            OutputName = output.OutputName
        };
        BindingDisplayText = $"\u2190 {output.StepDisplayName} \u00B7 {output.OutputDisplayName}";
    }

    [RelayCommand]
    private void Unbind()
    {
        Binding = null;
        BindingDisplayText = string.Empty;
    }

    public ParameterValue ToParameterValue()
    {
        var pv = new ParameterValue { Type = ParameterType };

        switch (ParameterType)
        {
            case ParameterType.Bool:
                pv.Value = BoolValue;
                break;
            case ParameterType.Int:
                pv.Value = int.TryParse(StringValue, out var i) ? i : 0;
                break;
            case ParameterType.Double:
                pv.Value = double.TryParse(StringValue, out var d) ? d : 0.0;
                break;
            case ParameterType.EnumChoice:
                pv.Value = SelectedEnumValue;
                break;
            case ParameterType.FilterSet:
                pv.FilterSet = FilterSetValue ?? new EntityFilterSet();
                break;
            case ParameterType.String:
            default:
                pv.Value = StringValue;
                break;
        }

        pv.Binding = Binding;
        return pv;
    }

    public static ParameterInputViewModel FromDescriptor(
        ParameterDescriptor descriptor, ParameterValue? existingValue = null)
    {
        var vm = new ParameterInputViewModel
        {
            Name = descriptor.Name,
            DisplayName = descriptor.DisplayName,
            Description = descriptor.Description,
            ParameterType = descriptor.Type,
            SupportsSampling = descriptor.SupportsSampling,
            EnumChoices = descriptor.EnumChoices ?? Array.Empty<string>(),
        };

        if (existingValue != null)
        {
            if (existingValue.Binding != null)
            {
                vm.Binding = existingValue.Binding;
                vm.BindingDisplayText = $"\u2190 {existingValue.Binding.SourceStepId} \u00B7 {existingValue.Binding.OutputName}";
            }

            switch (descriptor.Type)
            {
                case ParameterType.Bool:
                    vm.BoolValue = existingValue.Value is true;
                    break;
                case ParameterType.EnumChoice:
                    vm.SelectedEnumValue = existingValue.Value?.ToString() ?? string.Empty;
                    break;
                case ParameterType.String:
                case ParameterType.Int:
                case ParameterType.Double:
                    vm.StringValue = existingValue.Value?.ToString() ?? string.Empty;
                    break;
                case ParameterType.FilterSet:
                    if (existingValue.FilterSet != null)
                        vm.FilterSetValue = existingValue.FilterSet;
                    break;
            }
        }
        else if (descriptor.DefaultValue != null)
        {
            switch (descriptor.Type)
            {
                case ParameterType.Bool:
                    vm.BoolValue = descriptor.DefaultValue is true;
                    break;
                case ParameterType.EnumChoice:
                    vm.SelectedEnumValue = descriptor.DefaultValue.ToString() ?? string.Empty;
                    break;
                case ParameterType.String:
                case ParameterType.Int:
                case ParameterType.Double:
                    vm.StringValue = descriptor.DefaultValue.ToString() ?? string.Empty;
                    break;
            }
        }

        return vm;
    }
}
