using System.Windows.Controls;

using NSLOAD.ViewModels;

namespace NSLOAD.Views
{
    public partial class NsLoadPanel : UserControl
    {
        public NsLoadPanel()
        {
            InitializeComponent();
            DataContext = new NsLoadViewModel();
        }
    }
}
