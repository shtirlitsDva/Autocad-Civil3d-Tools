using System.Windows;

namespace DimensioneringV2.UI.PipeSettings
{
    /// <summary>
    /// Interaction logic for PipeSettingsWindow.xaml
    /// </summary>
    public partial class PipeSettingsWindow : Window
    {
        public PipeSettingsWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        }
    }
}
