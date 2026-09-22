using System.Windows.Controls;

using NSLOAD.ViewModels;

namespace NSLOAD.Views
{
    public partial class NsLoadPanel : UserControl
    {
        public NsLoadPanel()
        {
            InitializeComponent();
            var vm = new NsLoadViewModel();
            DataContext = vm;

            // Poll only while the drafter can see the rows.
            IsVisibleChanged += (_, e) =>
            {
                if ((bool)e.NewValue) vm.StartLiveRefresh();
                else vm.StopLiveRefresh();
            };
        }
    }
}
