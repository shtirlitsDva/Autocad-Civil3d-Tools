using System.Windows;

namespace DimensioneringV2.UI.PipeSettings
{
    public partial class RegelWindow : Window
    {
        public RegelWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        }
    }
}
