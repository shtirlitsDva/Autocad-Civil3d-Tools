using System.Windows;
using System.Windows.Input;

namespace DimensioneringV2.UI.ReportSettings;

public partial class ReportSettingsWindow : Window
{
    private readonly ReportSettingsViewModel _vm;

    public ReportSettingsWindow()
    {
        InitializeComponent();
        Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        _vm = new ReportSettingsViewModel();
        DataContext = _vm;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.ApplyAndClose();
        DialogResult = true;
    }

    private void ModuleItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ModuleToggleItem item)
        {
            foreach (var m in _vm.ModuleItems)
                m.IsSelected = false;
            item.IsSelected = true;
        }
    }
}
