using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.BatchProcessing.BPUIv2.UI.FilterEditor;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.SequenceComposer;

public partial class ParameterInputViewModel : ObservableObject
{
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

    public bool IsStringInput => ParameterType is ParameterType.String
        or ParameterType.Int or ParameterType.Double;

    public bool IsBoolInput => ParameterType == ParameterType.Bool;

    public bool IsEnumInput => ParameterType == ParameterType.EnumChoice;

    public bool IsSpecialInput => ParameterType is ParameterType.DataReferencesOptions
        or ParameterType.Counter or ParameterType.FilterSet;

    partial void OnParameterTypeChanged(ParameterType value)
    {
        OnPropertyChanged(nameof(IsStringInput));
        OnPropertyChanged(nameof(IsBoolInput));
        OnPropertyChanged(nameof(IsEnumInput));
        OnPropertyChanged(nameof(IsSpecialInput));
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
