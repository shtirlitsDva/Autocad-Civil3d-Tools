using SheetCreationAutomation.ViewModels;
using System.Windows.Controls;

namespace SheetCreationAutomation.UI
{
    public partial class DebugVisualTreeControl : UserControl
    {
        public DebugVisualTreeControl(DebugVisualTreeViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
