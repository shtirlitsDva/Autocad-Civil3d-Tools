using System.Windows;

namespace DimensioneringV2.UI.CalcManager;

public partial class SettingsBrowserDialog : Window
{
    public SettingsBrowserDialog(HydraulicSettings settings)
    {
        InitializeComponent();
        DataContext = new SettingsBrowserViewModel(settings);
    }
}
