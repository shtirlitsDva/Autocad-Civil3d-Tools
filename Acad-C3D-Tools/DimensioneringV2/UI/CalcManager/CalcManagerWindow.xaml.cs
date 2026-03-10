using System.Windows;

namespace DimensioneringV2.UI.CalcManager;

public partial class CalcManagerWindow : Window
{
    internal CalcManagerViewModel ViewModel { get; }

    public CalcManagerWindow()
    {
        InitializeComponent();
        ViewModel = new CalcManagerViewModel();
        DataContext = ViewModel;
        ViewModel.CloseRequested += (s, e) => Close();
    }
}
