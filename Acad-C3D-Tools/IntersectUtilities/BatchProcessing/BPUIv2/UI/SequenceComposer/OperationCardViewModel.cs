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
        {
            vm.Parameters.Add(ParameterInputViewModel.FromDescriptor(param));
        }

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
        };

        foreach (var param in entry.Parameters)
        {
            step.Parameters.TryGetValue(param.Name, out var existingValue);
            vm.Parameters.Add(
                ParameterInputViewModel.FromDescriptor(param, existingValue));
        }

        return vm;
    }
}
