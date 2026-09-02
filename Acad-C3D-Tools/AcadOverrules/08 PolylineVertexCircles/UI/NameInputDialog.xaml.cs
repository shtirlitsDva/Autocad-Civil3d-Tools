using System.Windows;
using System.Windows.Input;

using DimensioneringV2.UI;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// One line name prompt for the profile commands. A single field with OK/Cancel does not
    /// earn a view model, so the two controls are driven directly from here.
    /// </summary>
    public partial class NameInputDialog : Window
    {
        public NameInputDialog()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                DarkTitleBarHelper.EnableDarkTitleBar(this);
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        /// <summary>
        /// Shows the prompt. Returns the trimmed name, or null when the user cancelled or
        /// left the field empty.
        /// </summary>
        public static string? Prompt(string title, string caption, string initialValue)
        {
            var dialog = new NameInputDialog { Title = title };
            dialog.CaptionText.Text = caption;
            dialog.NameBox.Text = initialValue;

            ActiveWindow.Attach(dialog);

            if (dialog.ShowDialog() != true) return null;

            string name = dialog.NameBox.Text.Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }

        private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnNameBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            DialogResult = true;
            e.Handled = true;
        }
    }
}
