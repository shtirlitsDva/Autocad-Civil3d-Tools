using System.Windows;

namespace DimensioneringV2.UI.CalcManager;

public partial class CalcManagerWindow : Window
{
    internal CalcManagerViewModel ViewModel { get; }

    public CalcManagerWindow()
    {
        InitializeComponent();
        Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        Closed += (s, e) => ViewModel.Cleanup();
        ViewModel = new CalcManagerViewModel();
        DataContext = ViewModel;
        ViewModel.CloseRequested += (s, e) => Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
