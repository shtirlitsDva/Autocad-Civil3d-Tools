using System.Windows;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.DrawingList;

public partial class DrawingListDialog : Window
{
    public DrawingListDialog()
    {
        InitializeComponent();
        DataContext = new DrawingListViewModel();
    }

    public DrawingListViewModel ViewModel => (DrawingListViewModel)DataContext;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
