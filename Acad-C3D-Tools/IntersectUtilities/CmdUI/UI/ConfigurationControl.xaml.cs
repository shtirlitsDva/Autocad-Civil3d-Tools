using System.Windows.Controls;

using IntersectUtilities.UtilsCommon.DataManager.ConfigurationSelection;

namespace IntersectUtilities.CmdUI.UI
{
    /// <summary>
    /// Interaction logic for ConfigurationControl.xaml
    /// </summary>
    public partial class ConfigurationControl : UserControl
    {
        public ConfigurationControl()
        {
            InitializeComponent();
            DataContext = new ConfigurationViewModel();
        }

        /// <summary>
        /// Gets the ViewModel for this control.
        /// </summary>
        public ConfigurationViewModel ViewModel => (ConfigurationViewModel)DataContext;
    }
}
