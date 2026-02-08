using SheetCreationAutomation.ViewModels;
using System.Windows;
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

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is DebugVisualTreeViewModel vm && e.NewValue is WindowNodeViewModel node)
            {
                vm.SelectNode(node);
            }
        }
    }
}
