using System.Windows.Controls;
using DevReload.ViewModels;

namespace DevReload.Views
{
    public partial class DevReloadPanel : UserControl
    {
        public DevReloadPanel()
        {
            InitializeComponent();
            DataContext = new DevReloadViewModel();
        }
    }
}
