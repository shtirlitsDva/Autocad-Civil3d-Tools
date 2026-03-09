using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.BatchProcessing.BPUIv2.DrawingList;
using IntersectUtilities.BatchProcessing.BPUIv2.Registry;
using IntersectUtilities.BatchProcessing.BPUIv2.Sampling;
using IntersectUtilities.BatchProcessing.BPUIv2.Sequences;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.SequenceComposer;

public partial class SequenceComposerViewModel : ObservableObject
{
    private readonly OperationRegistry _registry;
    private readonly ObservableCollection<OperationCatalogEntry> _allCatalogEntries;
    private SampleResult? _cachedSample;

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
        WireCardDelegates(card);
        SequenceSteps.Add(card);
        RefreshAvailableOutputs();
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
            WireCardDelegates(card);
            SequenceSteps.Add(card);
        }

        RenumberSteps();
        RefreshAvailableOutputs();
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
                StepId = card.StepId,
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
        InvalidateBindingsAfterReorder();
        RefreshAvailableOutputs();
    }

    public void MoveStepDown(OperationCardViewModel card)
    {
        var idx = SequenceSteps.IndexOf(card);
        if (idx < 0 || idx >= SequenceSteps.Count - 1) return;
        SequenceSteps.Move(idx, idx + 1);
        RenumberSteps();
        InvalidateBindingsAfterReorder();
        RefreshAvailableOutputs();
    }

    public void RemoveStep(OperationCardViewModel card)
    {
        SequenceSteps.Remove(card);
        RenumberSteps();
        InvalidateBindingsAfterReorder();
        RefreshAvailableOutputs();
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

    private void WireCardDelegates(OperationCardViewModel card)
    {
        card.RequestMoveUp = MoveStepUp;
        card.RequestMoveDown = MoveStepDown;
        card.RequestRemove = RemoveStep;
        foreach (var p in card.Parameters)
            p.RequestSample = HandleSampleRequest;
    }

    private void HandleSampleRequest(ParameterInputViewModel param)
    {
        if (_cachedSample == null)
        {
            var items = DrawingListService.Instance.GetActiveItems();
            if (items.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "No drawings loaded. Add drawings to the drawing list first.",
                    "Sampling", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                _cachedSample = DrawingSampler.SampleFromDrawing(items[0].FilePath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to sample drawing:\n{ex}",
                    "Sampling Error", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }
        }

        string[] choices = ResolveSampleChoices(param.Name, _cachedSample);
        if (choices.Length == 0) return;

        param.EnumChoices = choices;
        param.ParameterType = Core.ParameterType.EnumChoice;
        if (choices.Length > 0 && string.IsNullOrEmpty(param.SelectedEnumValue))
            param.SelectedEnumValue = choices[0];
    }

    private static string[] ResolveSampleChoices(string paramName, SampleResult sample)
    {
        string key = paramName.ToLowerInvariant();

        if (key.Contains("layer")) return sample.LayerNames;
        if (key.Contains("textstyle") || key.Contains("text_style")) return sample.TextStyleNames;
        if (key.Contains("block")) return sample.BlockNames;
        if (key.Contains("linetype")) return sample.LinetypeNames;
        if (key.Contains("alignmentstyle") || key.Contains("alignment_style")) return sample.AlignmentStyleNames;
        if (key.Contains("profilestyle") || key.Contains("profile_style")) return sample.ProfileStyleNames;
        if (key.Contains("profileviewstyle") || key.Contains("profile_view_style")) return sample.ProfileViewStyleNames;
        if (key.Contains("bandset") || key.Contains("band_set")) return sample.ProfileViewBandSetStyleNames;

        return sample.LayerNames;
    }

    public event Action? SequenceSaved;

    [RelayCommand]
    private void SaveAsUserSequence()
    {
        if (string.IsNullOrWhiteSpace(SequenceName))
        {
            System.Windows.MessageBox.Show(
                "Please enter a sequence name.",
                "Save Sequence", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var definition = ToSequenceDefinition();
        SequenceStorageService.Instance.SaveUserSequence(definition);
        SequenceSaved?.Invoke();

        System.Windows.MessageBox.Show(
            $"Sequence '{SequenceName}' saved.",
            "Save Sequence", System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void InvalidateBindingsAfterReorder()
    {
        var stepPositions = new Dictionary<string, int>();
        for (int i = 0; i < SequenceSteps.Count; i++)
            stepPositions[SequenceSteps[i].StepId] = i;

        for (int i = 0; i < SequenceSteps.Count; i++)
        {
            foreach (var param in SequenceSteps[i].Parameters)
            {
                if (param.Binding == null) continue;

                if (!stepPositions.TryGetValue(param.Binding.SourceStepId, out int sourcePos)
                    || sourcePos >= i)
                {
                    param.UnbindCommand.Execute(null);
                }
            }
        }
    }

    private void RefreshAvailableOutputs()
    {
        for (int i = 0; i < SequenceSteps.Count; i++)
        {
            var precedingOutputs = new List<AvailableOutput>();

            for (int j = 0; j < i; j++)
            {
                var precedingStep = SequenceSteps[j];
                foreach (var output in precedingStep.Outputs)
                {
                    precedingOutputs.Add(new AvailableOutput(
                        precedingStep.StepId,
                        $"#{precedingStep.Index} {precedingStep.DisplayName}",
                        output.Name,
                        output.DisplayName,
                        output.Type));
                }
            }

            foreach (var param in SequenceSteps[i].Parameters)
            {
                param.AvailableOutputs.Clear();
                foreach (var avail in precedingOutputs)
                {
                    if (avail.Type == param.ParameterType)
                        param.AvailableOutputs.Add(avail);
                }

                if (param.Binding != null)
                {
                    var match = precedingOutputs.FirstOrDefault(
                        o => o.StepId == param.Binding.SourceStepId
                             && o.OutputName == param.Binding.OutputName);
                    if (match != null)
                        param.BindingDisplayText = $"\u2190 {match.StepDisplayName} \u00B7 {match.OutputDisplayName}";
                }
            }
        }
    }

    private void RenumberSteps()
    {
        for (int i = 0; i < SequenceSteps.Count; i++)
            SequenceSteps[i].Index = i + 1;
    }
}
