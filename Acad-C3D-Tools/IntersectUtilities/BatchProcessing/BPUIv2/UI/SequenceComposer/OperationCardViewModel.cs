using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.BatchProcessing.BPUIv2.Registry;
using IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.SequenceComposer;

public partial class OperationCardViewModel : ObservableObject
{
    [ObservableProperty]
    private string typeId = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string category = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ParameterInputViewModel> parameters = new();

    [ObservableProperty]
    private ObservableCollection<OutputDisplayItem> outputs = new();

    [ObservableProperty]
    private string stepId = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private int index;

    public Action<OperationCardViewModel>? RequestMoveUp { get; set; }
    public Action<OperationCardViewModel>? RequestMoveDown { get; set; }
    public Action<OperationCardViewModel>? RequestRemove { get; set; }

    [RelayCommand]
    private void MoveUp() => RequestMoveUp?.Invoke(this);

    [RelayCommand]
    private void MoveDown() => RequestMoveDown?.Invoke(this);

    [RelayCommand]
    private void Remove() => RequestRemove?.Invoke(this);

    public static OperationCardViewModel FromCatalogEntry(OperationCatalogEntry entry)
    {
        var vm = new OperationCardViewModel
        {
            TypeId = entry.TypeId,
            DisplayName = entry.DisplayName,
            Description = entry.Description,
            Category = entry.Category,
        };

        foreach (var param in entry.Parameters)
            vm.Parameters.Add(ParameterInputViewModel.FromDescriptor(param));

        foreach (var output in entry.Outputs)
            vm.Outputs.Add(new OutputDisplayItem(output.Name, output.DisplayName, output.Type));

        return vm;
    }

    public static OperationCardViewModel FromOperationStep(
        OperationStep step, OperationCatalogEntry entry)
    {
        var vm = new OperationCardViewModel
        {
            TypeId = entry.TypeId,
            DisplayName = entry.DisplayName,
            Description = entry.Description,
            Category = entry.Category,
            StepId = step.StepId,
        };

        foreach (var param in entry.Parameters)
        {
            step.Parameters.TryGetValue(param.Name, out var existingValue);
            vm.Parameters.Add(
                ParameterInputViewModel.FromDescriptor(param, existingValue));
        }

        foreach (var output in entry.Outputs)
            vm.Outputs.Add(new OutputDisplayItem(output.Name, output.DisplayName, output.Type));

        return vm;
    }
}
