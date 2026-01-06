using DimensioneringV2.Models.Nyttetimer;

using System.Collections.ObjectModel;
using System.Windows;

namespace DimensioneringV2.UI.Nyttetimer
{
    public partial class NewConfigurationDialog : Window
    {
        public string ConfigurationName => NameTextBox.Text;
        public NyttetimerConfiguration? SelectedTemplate => TemplateComboBox.SelectedItem as NyttetimerConfiguration;

        public NewConfigurationDialog(ObservableCollection<NyttetimerConfiguration> templates)
        {
            InitializeComponent();
            TemplateComboBox.ItemsSource = templates;
            TemplateComboBox.SelectedIndex = 0; // Default to first (Standard)
            NameTextBox.Text = "Ny konfiguration";
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ConfigurationName))
            {
                MessageBox.Show("Angiv venligst et navn til konfigurationen.",
                    "Navn påkrævet", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedTemplate == null)
            {
                MessageBox.Show("Vælg venligst en skabelon.",
                    "Skabelon påkrævet", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

