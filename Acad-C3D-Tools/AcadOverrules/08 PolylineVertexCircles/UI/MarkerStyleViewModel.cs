using CommunityToolkit.Mvvm.ComponentModel;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// The editable state of one <see cref="MarkerStyle"/> - the look of the circle for one
    /// class of vertex. A profile owns two of these, so the two vertex tabs of the Appearance
    /// group are the same template bound to different instances.
    /// </summary>
    public partial class MarkerStyleViewModel : StyleViewModelBase
    {
        [ObservableProperty]
        private string radiusText;

        [ObservableProperty]
        private bool useFixedColor;

        private double _radius;

        public MarkerStyleViewModel(MarkerStyle style)
            : base(style.LineWeightFactor, style.FixedColor, style.Linetype)
        {
            _radius = style.Radius;
            radiusText = NumberText.Format(_radius);
            useFixedColor = style.ColorMode == MarkerColorMode.FixedColor;
        }

        public override bool IsValid => IsRadiusValid && IsLineWeightFactorValid;

        public bool IsRadiusValid =>
            NumberText.TryParse(RadiusText, out double radius) && radius > 0.0;

        public MarkerStyle ToModel() =>
            new MarkerStyle
            {
                Radius = _radius,
                LineWeightFactor = LineWeightFactor,
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

        protected override void OnFixedColorPicked() => UseFixedColor = true;

        partial void OnRadiusTextChanged(string value)
        {
            if (NumberText.TryParse(value, out double radius) && radius > 0.0)
                _radius = radius;

            OnPropertyChanged(nameof(IsRadiusValid));
            OnPropertyChanged(nameof(IsValid));
        }
    }
}
