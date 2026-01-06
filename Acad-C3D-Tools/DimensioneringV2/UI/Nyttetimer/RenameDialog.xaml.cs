using System.Windows;

namespace DimensioneringV2.UI.Nyttetimer
{
    public partial class RenameDialog : Window
    {
        public string NewName => NameTextBox.Text;

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NameTextBox.Text = currentName;
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewName))
            {
                MessageBox.Show("Angiv venligst et navn.",
                    "Navn påkrævet", MessageBoxButton.OK, MessageBoxImage.Warning);
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

