using System.Windows;

using DimensioneringV2.UI;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// Settings window for <see cref="PolylineVertexCircles"/>, opened by
    /// TOGGLEPOLYVERTICESSETTINGS. The active profile is written to disk when the window
    /// closes, however it is closed.
    /// </summary>
    public partial class VertexCirclesSettingsWindow : Window
    {
        public VertexCirclesSettingsWindow()
        {
            InitializeComponent();
            DataContext = new VertexCirclesSettingsViewModel();
            Loaded += (_, _) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        }

        public VertexCirclesSettingsViewModel? ViewModel =>
            DataContext as VertexCirclesSettingsViewModel;

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(System.EventArgs e)
        {
            ViewModel?.SaveOnExit();
            base.OnClosed(e);
        }
    }
}
