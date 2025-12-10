using System.Windows.Controls;

namespace DimensioneringV2.UI
{
    public partial class GASettingsTab : UserControl
    {
        private readonly GASettingsTabViewModel vm;

        public GASettingsTab()
        {
            InitializeComponent();
            vm = new GASettingsTabViewModel();
            DataContext = vm;
        }
    }
}
