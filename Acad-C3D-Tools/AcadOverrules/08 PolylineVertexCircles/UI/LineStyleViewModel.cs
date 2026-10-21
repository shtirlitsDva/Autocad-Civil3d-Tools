using System;
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// One entry in the colour mode dropdown of a line style.
    /// </summary>
    public sealed record SegmentColorModeChoice(SegmentColorMode Mode, string Label);

    /// <summary>
    /// The editable state of one <see cref="LineStyle"/>: colour mode, lineweight factor and
    /// linetype, each with a "same as the polyline" value. Used as is for the tangent lines
    /// and extended by <see cref="SegmentStyleViewModel"/> for the segment overrides.
    /// </summary>
    public partial class LineStyleViewModel : StyleViewModelBase
    {
        /// <summary>
        /// The dropdown entry that stands for <see cref="LineStyle.PolylineLinetype"/>: a
        /// ComboBox cannot show an empty string as a choice, so the empty name is shown under
        /// this label and mapped back in <see cref="ToModel"/>.
        /// </summary>
        public const string PolylineLinetypeChoice = "Same as the polyline";

        public static IReadOnlyList<SegmentColorModeChoice> ColorModes { get; } =
            new[]
            {
                new SegmentColorModeChoice(SegmentColorMode.Polyline, "Same as the polyline"),
                new SegmentColorModeChoice(SegmentColorMode.ComplementaryHue, "Complementary hue"),
                new SegmentColorModeChoice(SegmentColorMode.FixedColor, "Fixed colour"),
            };

        [ObservableProperty]
        private SegmentColorMode colorMode;

        public LineStyleViewModel(LineStyle style)
            : base(style.LineWeightFactor, style.FixedColor,
                style.UsesPolylineLinetype ? PolylineLinetypeChoice : style.Linetype)
        {
            colorMode = style.ColorMode;
        }

        public override bool IsValid => IsLineWeightFactorValid;

        /// <summary>Enables the swatch and the picker only when the fixed colour is in use.</summary>
        public bool IsFixedColor => ColorMode == SegmentColorMode.FixedColor;

        public LineStyle ToModel() => CopyTo(new LineStyle());

        /// <summary>True when <paramref name="name"/> is the placeholder or nothing at all.</summary>
        public static bool IsPolylineLinetypeChoice(string? name) =>
            string.IsNullOrWhiteSpace(name) ||
            string.Equals(name.Trim(), PolylineLinetypeChoice, StringComparison.Ordinal);

        /// <summary>
        /// The linetype name to store: the empty name for the placeholder. A ComboBox with no
        /// match sets the bound value to null; the polyline's own linetype is the only
        /// sensible reading of "no linetype chosen".
        /// </summary>
        public string LinetypeToStore() =>
            IsPolylineLinetypeChoice(Linetype) ? LineStyle.PolylineLinetype : Linetype.Trim();

        protected T CopyTo<T>(T target) where T : LineStyle
        {
            target.ColorMode = ColorMode;
            target.FixedColor = FixedColor;
            target.LineWeightFactor = LineWeightFactor;
            target.Linetype = LinetypeToStore();
            return target;
        }

        protected override void OnFixedColorPicked() => ColorMode = SegmentColorMode.FixedColor;

        partial void OnColorModeChanged(SegmentColorMode value) =>
            OnPropertyChanged(nameof(IsFixedColor));
    }
}
