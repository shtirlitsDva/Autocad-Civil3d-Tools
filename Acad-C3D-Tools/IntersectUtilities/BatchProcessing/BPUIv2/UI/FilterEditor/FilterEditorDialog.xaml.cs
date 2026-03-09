using System.Windows;
using DimensioneringV2.UI;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.FilterEditor;

public partial class FilterEditorDialog : Window
{
    public FilterEditorViewModel? ViewModel { get; }

    public FilterEditorDialog(EntityFilterSet? existing = null)
    {
        try
        {
            ViewModel = new FilterEditorViewModel();
            DataContext = ViewModel;
            InitializeComponent();
            ViewModel.LoadFrom(existing);
            Loaded += (_, _) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        }
        catch (Exception ex)
        {
            prdDbg($"BPUIv2: Failed to initialize FilterEditorDialog: {ex}");
            MessageBox.Show(
                $"Failed to open Filter Editor:\n{ex}",
                "BPv2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    public EntityFilterSet ResultFilterSet =>
        ViewModel?.ToFilterSet() ?? new EntityFilterSet();

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
