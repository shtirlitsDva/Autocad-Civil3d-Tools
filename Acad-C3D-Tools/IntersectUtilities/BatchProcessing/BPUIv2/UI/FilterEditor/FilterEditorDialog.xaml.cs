using System.Windows;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.FilterEditor;

public partial class FilterEditorDialog : Window
{
    public FilterEditorViewModel ViewModel { get; }

    public FilterEditorDialog(EntityFilterSet? existing = null)
    {
        ViewModel = new FilterEditorViewModel();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.LoadFrom(existing);
    }

    public EntityFilterSet ResultFilterSet => ViewModel.ToFilterSet();

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
