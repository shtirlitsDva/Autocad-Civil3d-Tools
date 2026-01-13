using System.Windows;

namespace DimensioneringV2.UI.PipeSettings
{
    /// <summary>
    /// Interaction logic for AcceptCriteriaWindow.xaml
    /// </summary>
    public partial class AcceptCriteriaWindow : Window
    {
        public AcceptCriteriaWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        }
    }
}
