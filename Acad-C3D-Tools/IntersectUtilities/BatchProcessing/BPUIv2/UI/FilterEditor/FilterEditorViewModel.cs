using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.FilterEditor;

public partial class FilterEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FilterGroupViewModel> groups = new();

    public FilterProperty[] AvailableProperties { get; } =
        Enum.GetValues<FilterProperty>();

    public FilterOperator[] AvailableOperators { get; } =
        Enum.GetValues<FilterOperator>();

    public void LoadFrom(EntityFilterSet? filterSet)
    {
        Groups.Clear();
        if (filterSet == null) return;

        foreach (var group in filterSet.Groups)
        {
            var gvm = new FilterGroupViewModel();
            foreach (var condition in group.Conditions)
            {
                gvm.Conditions.Add(new FilterConditionViewModel
                {
                    Property = condition.Property,
                    Operator = condition.Operator,
                    Value = condition.Value
                });
            }
            Groups.Add(gvm);
        }
    }

    public EntityFilterSet ToFilterSet()
    {
        var set = new EntityFilterSet();
        foreach (var gvm in Groups)
        {
            var group = new EntityFilterGroup();
            foreach (var cvm in gvm.Conditions)
            {
                group.Conditions.Add(new EntityFilter
                {
                    Property = cvm.Property,
                    Operator = cvm.Operator,
                    Value = cvm.Value
                });
            }
            set.Groups.Add(group);
        }
        return set;
    }

    [RelayCommand]
    private void AddGroup()
    {
        var group = new FilterGroupViewModel();
        group.Conditions.Add(new FilterConditionViewModel());
        Groups.Add(group);
    }

    [RelayCommand]
    private void RemoveGroup(FilterGroupViewModel group)
    {
        Groups.Remove(group);
    }
}

public partial class FilterGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FilterConditionViewModel> conditions = new();

    [RelayCommand]
    private void AddCondition()
    {
        Conditions.Add(new FilterConditionViewModel());
    }

    [RelayCommand]
    private void RemoveCondition(FilterConditionViewModel condition)
    {
        Conditions.Remove(condition);
    }
}

public partial class FilterConditionViewModel : ObservableObject
{
    [ObservableProperty]
    private FilterProperty property;

    [ObservableProperty]
    private FilterOperator @operator;

    [ObservableProperty]
    private string value = string.Empty;
}
