using System.Collections.Generic;
using System.Windows;

using DimensioneringV2.UI;

namespace AcadOverrules.VertexCircles.UI
{
    public partial class LayerPickerDialog : Window
    {
        public LayerPickerDialog()
        {
            InitializeComponent();
            DataContext = new LayerPickerViewModel();
            Loaded += (_, _) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        }

        public LayerPickerViewModel? ViewModel => DataContext as LayerPickerViewModel;

        /// <summary>
        /// Shows the picker over the active window. Returns the ticked layer names, or null
        /// when the user cancelled.
        /// </summary>
        public static IReadOnlyList<string>? PickLayers()
        {
            var dialog = new LayerPickerDialog();
            ActiveWindow.Attach(dialog);

            if (dialog.ViewModel?.HasLayers == false)
            {
                MessageBox.Show(
                    "No drawing is open, so there are no layers to pick from.",
                    "Polyline vertex circles", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            return dialog.ShowDialog() == true ? dialog.ViewModel?.SelectedLayerNames : null;
        }

        private void OnAddClick(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
