using System.Windows;
using DimensioneringV2.UI;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.DrawingList;

public partial class DrawingListDialog : Window
{
    public DrawingListDialog()
    {
        try
        {
            InitializeComponent();
            DataContext = new DrawingListViewModel();
            Loaded += (_, _) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        }
        catch (Exception ex)
        {
            prdDbg($"BPUIv2: Failed to initialize DrawingListDialog: {ex}");
            MessageBox.Show(
                $"Failed to open Drawing List:\n{ex}",
                "BPv2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    public DrawingListViewModel? ViewModel =>
        DataContext as DrawingListViewModel;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
