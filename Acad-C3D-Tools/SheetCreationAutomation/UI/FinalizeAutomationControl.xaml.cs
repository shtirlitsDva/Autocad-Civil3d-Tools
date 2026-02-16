using SheetCreationAutomation.ViewModels;
using System.Windows.Controls;

namespace SheetCreationAutomation.UI
{
    public partial class FinalizeAutomationControl : UserControl
    {
        public FinalizeAutomationControl(FinalizeAutomationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
