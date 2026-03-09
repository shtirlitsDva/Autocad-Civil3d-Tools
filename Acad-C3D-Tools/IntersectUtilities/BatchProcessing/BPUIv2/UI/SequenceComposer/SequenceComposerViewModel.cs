using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.BatchProcessing.BPUIv2.Registry;
using IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.SequenceComposer;

public partial class SequenceComposerViewModel : ObservableObject
{
    private readonly OperationRegistry _registry;
    private readonly ObservableCollection<OperationCatalogEntry> _allCatalogEntries;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<OperationCatalogEntry> catalogEntries = new();

    [ObservableProperty]
    private ObservableCollection<OperationCardViewModel> sequenceSteps = new();

    [ObservableProperty]
    private string sequenceName = string.Empty;

    [ObservableProperty]
    private string sequenceDescription = string.Empty;

    [ObservableProperty]
    private string sequenceCategory = string.Empty;

    public ICollectionView GroupedCatalog { get; }

    public SequenceComposerViewModel()
    {
        _registry = OperationRegistry.Instance;
        _allCatalogEntries = new ObservableCollection<OperationCatalogEntry>(_registry.Catalog);

        foreach (var entry in _allCatalogEntries)
            CatalogEntries.Add(entry);

        GroupedCatalog = CollectionViewSource.GetDefaultView(CatalogEntries);
        GroupedCatalog.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(OperationCatalogEntry.Category)));
        GroupedCatalog.SortDescriptions.Add(
            new SortDescription(nameof(OperationCatalogEntry.Category), ListSortDirection.Ascending));
        GroupedCatalog.SortDescriptions.Add(
            new SortDescription(nameof(OperationCatalogEntry.DisplayName), ListSortDirection.Ascending));
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterCatalog();
    }

    [RelayCommand]
    private void AddOperation(OperationCatalogEntry entry)
    {
        var card = OperationCardViewModel.FromCatalogEntry(entry);
        card.Index = SequenceSteps.Count + 1;
        card.RequestMoveUp = MoveStepUp;
        card.RequestMoveDown = MoveStepDown;
        card.RequestRemove = RemoveStep;
        SequenceSteps.Add(card);
    }

    [RelayCommand]
    private void ClearSequence()
    {
        SequenceSteps.Clear();
    }

    public void LoadFromSequence(SequenceDefinition sequence)
    {
        SequenceSteps.Clear();
        SequenceName = sequence.Name;
        SequenceDescription = sequence.Description;
        SequenceCategory = sequence.Category;

        foreach (var step in sequence.Steps)
        {
            var catalogEntry = _allCatalogEntries
                .FirstOrDefault(e => e.TypeId == step.OperationTypeId);
            if (catalogEntry == null) continue;

            var card = OperationCardViewModel.FromOperationStep(step, catalogEntry);
            card.RequestMoveUp = MoveStepUp;
            card.RequestMoveDown = MoveStepDown;
            card.RequestRemove = RemoveStep;
            SequenceSteps.Add(card);
        }

        RenumberSteps();
    }

    public SequenceDefinition ToSequenceDefinition()
    {
        var definition = new SequenceDefinition
        {
            Name = SequenceName,
            Description = SequenceDescription,
            Category = SequenceCategory,
            StorageLevel = SequenceStorageLevel.User,
            Steps = new List<OperationStep>()
        };

        foreach (var card in SequenceSteps)
        {
            var step = new OperationStep
            {
                OperationTypeId = card.TypeId,
                Parameters = new Dictionary<string, ParameterValue>()
            };

            foreach (var param in card.Parameters)
            {
                step.Parameters[param.Name] = param.ToParameterValue();
            }

            definition.Steps.Add(step);
        }

        return definition;
    }

    public void MoveStepUp(OperationCardViewModel card)
    {
        var idx = SequenceSteps.IndexOf(card);
        if (idx <= 0) return;
        SequenceSteps.Move(idx, idx - 1);
        RenumberSteps();
    }

    public void MoveStepDown(OperationCardViewModel card)
    {
        var idx = SequenceSteps.IndexOf(card);
        if (idx < 0 || idx >= SequenceSteps.Count - 1) return;
        SequenceSteps.Move(idx, idx + 1);
        RenumberSteps();
    }

    public void RemoveStep(OperationCardViewModel card)
    {
        SequenceSteps.Remove(card);
        RenumberSteps();
    }

    public void FilterCatalog()
    {
        CatalogEntries.Clear();

        var filter = SearchText?.Trim() ?? string.Empty;

        foreach (var entry in _allCatalogEntries)
        {
            if (string.IsNullOrEmpty(filter)
                || entry.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || entry.Category.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || entry.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                CatalogEntries.Add(entry);
            }
        }

        GroupedCatalog.Refresh();
    }

    private void RenumberSteps()
    {
        for (int i = 0; i < SequenceSteps.Count; i++)
            SequenceSteps[i].Index = i + 1;
    }
}
