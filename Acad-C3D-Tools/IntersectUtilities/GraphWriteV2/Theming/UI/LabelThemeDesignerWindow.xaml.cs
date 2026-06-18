using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Microsoft.Win32;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.GraphWriteV2.Theming.UI
{
    /// <summary>
    /// The GRAPHWRITEV2THEME settings window. Custom chrome (WindowStyle=None) reproducing the design
    /// package mock; the code-behind owns only the window-shell concerns (drag, min/max/close), the
    /// native color picker, and the footer actions. All theme state lives in
    /// <see cref="LabelThemeDesignerViewModel"/>. "Apply" persists via <see cref="LabelThemeStore"/>.
    /// </summary>
    public partial class LabelThemeDesignerWindow : Window
    {
        private readonly LabelThemeDesignerViewModel _vm;

        /// <summary>True when the user pressed Apply (theme saved); false on Cancel/close.</summary>
        public bool Applied { get; private set; }

        public LabelThemeDesignerWindow(LabelTheme theme)
        {
            InitializeComponent();
            _vm = new LabelThemeDesignerViewModel(theme);
            DataContext = _vm;
            Loaded += (_, _) => _vm.Start();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;

            var seed = System.Drawing.Color.Black;
            if (btn.Background is SolidColorBrush scb)
                seed = System.Drawing.Color.FromArgb(scb.Color.R, scb.Color.G, scb.Color.B);

            using var dlg = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                Color = seed,
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string hex = $"#{dlg.Color.R:x2}{dlg.Color.G:x2}{dlg.Color.B:x2}";
                _vm.SetColor(key, hex);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export label theme",
                Filter = "JSON theme (*.json)|*.json",
                FileName = "label-theme.json",
            };
            if (dlg.ShowDialog(this) == true)
            {
                try
                {
                    LabelThemeStore.Export(_vm.BuildTheme(), dlg.FileName);
                }
                catch (System.Exception ex)
                {
                    prdDbg($"GRAPHWRITEV2THEME: export failed.\n{ex.Message}");
                    MessageBox.Show(this, $"Export failed:\n{ex.Message}", "Label Theme Designer",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LabelThemeStore.Save(_vm.BuildTheme());
                Applied = true;
                Close();
            }
            catch (System.Exception ex)
            {
                prdDbg($"GRAPHWRITEV2THEME: failed to save theme.\n{ex.Message}");
                MessageBox.Show(this, $"Could not save theme:\n{ex.Message}", "Label Theme Designer",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
