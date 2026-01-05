using System.Windows;

namespace DimensioneringV2.UI
{
    public partial class RoughnessSettingsWindow : Window
    {
        public RoughnessSettingsWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
