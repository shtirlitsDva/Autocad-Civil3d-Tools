using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// The editable state of one <see cref="MarkerStyle"/> - the look of the circle for one
    /// class of vertex. A profile owns two of these, so the two tabs of the Circle group are
    /// the same template bound to different instances.
    ///
    /// The numeric fields are held as text because the user types into them: the parsed value
    /// only moves when the text parses, so the overrule keeps drawing the last good radius
    /// while a number is half typed.
    /// </summary>
    public partial class MarkerStyleViewModel : ObservableObject
    {
        [ObservableProperty]
        private string radiusText;

        [ObservableProperty]
        private string lineWeightFactorText;

        [ObservableProperty]
        private bool useFixedColor;

        [ObservableProperty]
        private string fixedColor;

        /// <summary>
        /// Name of the linetype the circle is drawn with. Bound to a ComboBox, so the value
        /// is always one of the names the window offers - see
        /// <see cref="VertexCirclesSettingsViewModel.AvailableLinetypes"/>.
        /// </summary>
        [ObservableProperty]
        private string linetype;

        private double _radius;
        private double _lineWeightFactor;

        public MarkerStyleViewModel(MarkerStyle style)
        {
            _radius = style.Radius;
            _lineWeightFactor = style.LineWeightFactor;

            radiusText = NumberText.Format(_radius);
            lineWeightFactorText = NumberText.Format(_lineWeightFactor);

            useFixedColor = style.ColorMode == MarkerColorMode.FixedColor;
            fixedColor = style.FixedColor;
            linetype = style.Linetype;
        }

        /// <summary>True while both numeric fields hold something parseable and positive.</summary>
        public bool IsValid => IsRadiusValid && IsLineWeightFactorValid;

        public bool IsRadiusValid =>
            NumberText.TryParse(RadiusText, out double radius) && radius > 0.0;

        public bool IsLineWeightFactorValid =>
            NumberText.TryParse(LineWeightFactorText, out double factor) && factor > 0.0;

        /// <summary>The colour swatch shown on the fixed colour button.</summary>
        public Brush FixedColorBrush
        {
            get
            {
                System.Drawing.Color rgb = HtmlColor.Parse(FixedColor);
                return new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
            }
        }

        public MarkerStyle ToModel() =>
            new MarkerStyle
            {
                Radius = _radius,
                LineWeightFactor = _lineWeightFactor,
                ColorMode = UseFixedColor
                    ? MarkerColorMode.FixedColor
                    : MarkerColorMode.ComplementaryHue,
                FixedColor = FixedColor,
                //A ComboBox with no match sets the bound value to null; the default is
                //the only sensible reading of "no linetype chosen".
                Linetype = string.IsNullOrWhiteSpace(Linetype)
                    ? MarkerStyle.DefaultLinetype
                    : Linetype.Trim(),
            };

        [RelayCommand]
        private void PickFixedColor()
        {
            var dialog = new Autodesk.AutoCAD.Windows.ColorDialog();
            dialog.Color = ToAcadColor(FixedColor);

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            //ColorValue resolves ACI and true colour alike, so any tab of the dialog works.
            FixedColor = HtmlColor.Format(dialog.Color.ColorValue);
            UseFixedColor = true;
        }

        //Keep the parsed values in step with the text, but never let unparseable input
        //destroy the last good value - the overrule keeps drawing while the user types.
        partial void OnRadiusTextChanged(string value)
        {
            if (NumberText.TryParse(value, out double radius) && radius > 0.0)
                _radius = radius;

            OnPropertyChanged(nameof(IsRadiusValid));
            OnPropertyChanged(nameof(IsValid));
        }

        partial void OnLineWeightFactorTextChanged(string value)
        {
            if (NumberText.TryParse(value, out double factor) && factor > 0.0)
                _lineWeightFactor = factor;

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
