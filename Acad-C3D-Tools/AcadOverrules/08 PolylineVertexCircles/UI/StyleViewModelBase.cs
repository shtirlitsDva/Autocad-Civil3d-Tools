using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// What a vertex circle style and a segment style have in common: a lineweight factor, a
    /// fixed colour with its picker, and a linetype name. The two derived view models add
    /// what is specific to each.
    ///
    /// The numeric fields are held as text because the user types into them: the parsed value
    /// only moves when the text parses, so the overrule keeps drawing the last good value
    /// while a number is half typed.
    /// </summary>
    public abstract partial class StyleViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private string lineWeightFactorText;

        [ObservableProperty]
        private string fixedColor;

        /// <summary>
        /// Name of the linetype. Bound to a ComboBox, so the value is always one of the names
        /// the window offers - see
        /// <see cref="VertexCirclesSettingsViewModel.AvailableLinetypes"/>.
        /// </summary>
        [ObservableProperty]
        private string linetype;

        protected StyleViewModelBase(double lineWeightFactor, string fixedColor, string linetype)
        {
            LineWeightFactor = lineWeightFactor;
            lineWeightFactorText = NumberText.Format(lineWeightFactor);
            this.fixedColor = fixedColor;
            this.linetype = linetype;
        }

        /// <summary>The last lineweight factor that parsed.</summary>
        protected double LineWeightFactor { get; private set; }

        /// <summary>True while every numeric field holds something parseable and positive.</summary>
        public abstract bool IsValid { get; }

        public bool IsLineWeightFactorValid =>
            NumberText.TryParse(LineWeightFactorText, out double factor) && factor > 0.0;

        /// <summary>The colour swatch shown next to the fixed colour button.</summary>
        public Brush FixedColorBrush
        {
            get
            {
                System.Drawing.Color rgb = HtmlColor.Parse(FixedColor);
                return new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
            }
        }

        [RelayCommand]
        private void PickFixedColor()
        {
            var dialog = new Autodesk.AutoCAD.Windows.ColorDialog();
            dialog.Color = ToAcadColor(FixedColor);

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            //ColorValue resolves ACI and true colour alike, so any tab of the dialog works.
            FixedColor = HtmlColor.Format(dialog.Color.ColorValue);
            OnFixedColorPicked();
        }

        /// <summary>
        /// Picking a colour is a clear statement that the user wants it used, so the derived
        /// style switches its colour mode to the fixed colour here.
        /// </summary>
        protected abstract void OnFixedColorPicked();

        //Keep the parsed value in step with the text, but never let unparseable input destroy
        //the last good value - the overrule keeps drawing while the user types.
        partial void OnLineWeightFactorTextChanged(string value)
        {
            if (NumberText.TryParse(value, out double factor) && factor > 0.0)
                LineWeightFactor = factor;

            OnPropertyChanged(nameof(IsLineWeightFactorValid));
            OnPropertyChanged(nameof(IsValid));
        }

        partial void OnFixedColorChanged(string value) =>
            OnPropertyChanged(nameof(FixedColorBrush));

        private static Autodesk.AutoCAD.Colors.Color ToAcadColor(string html)
        {
            System.Drawing.Color rgb = HtmlColor.Parse(html);
            return Autodesk.AutoCAD.Colors.Color.FromRgb(rgb.R, rgb.G, rgb.B);
        }
    }
}
