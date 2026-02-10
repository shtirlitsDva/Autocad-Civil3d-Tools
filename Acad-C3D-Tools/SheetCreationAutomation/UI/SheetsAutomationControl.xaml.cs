using SheetCreationAutomation.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace SheetCreationAutomation.UI
{
    public partial class SheetsAutomationControl : UserControl
    {
        public SheetsAutomationControl(SheetsAutomationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CoordinatesOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextCoordinateValid(e.Text);
        }

        private static bool IsTextCoordinateValid(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsDigit(c) && c != ',')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
