using SheetCreationAutomation.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace SheetCreationAutomation.UI
{
    public partial class ViewFramesAutomationControl : UserControl
    {        
        public ViewFramesAutomationControl(ViewFramesAutomationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllNumeric(e.Text);
        }

        private static bool IsTextAllNumeric(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
