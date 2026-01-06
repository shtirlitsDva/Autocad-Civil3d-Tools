using System.Windows;

namespace DimensioneringV2.UI.Nyttetimer
{
    public partial class ConfigurationSelectorWindow : Window
    {
        public ConfigurationSelectorWindow()
        {
            InitializeComponent();
            DataContext = new ConfigurationSelectorViewModel(() => Close());
        }
    }
}

