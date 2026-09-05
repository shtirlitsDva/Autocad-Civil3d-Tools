using System;
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// One entry in the colour mode dropdown of a segment tab.
    /// </summary>
    public sealed record SegmentColorModeChoice(SegmentColorMode Mode, string Label);

    /// <summary>
    /// The editable state of one <see cref="SegmentStyle"/> - how one class of segment is
    /// drawn when the profile overrides it. A profile owns two of these, so the two segment
    /// tabs of the Appearance group are the same template bound to different instances.
    /// </summary>
    public partial class SegmentStyleViewModel : StyleViewModelBase
    {
        /// <summary>
        /// The dropdown entry that stands for <see cref="SegmentStyle.PolylineLinetype"/>: a
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
        private bool isOverridden;

        [ObservableProperty]
        private SegmentColorMode colorMode;

        public SegmentStyleViewModel(SegmentStyle style)
            : base(style.LineWeightFactor, style.FixedColor,
                style.UsesPolylineLinetype ? PolylineLinetypeChoice : style.Linetype)
        {
            isOverridden = style.Override;
            colorMode = style.ColorMode;
        }

        public override bool IsValid => IsLineWeightFactorValid;

        /// <summary>Enables the swatch and the picker only when the fixed colour is in use.</summary>
        public bool IsFixedColor => ColorMode == SegmentColorMode.FixedColor;

        public SegmentStyle ToModel() =>
            new SegmentStyle
            {
                Override = IsOverridden,
                ColorMode = ColorMode,
                FixedColor = FixedColor,
                LineWeightFactor = LineWeightFactor,
                //A ComboBox with no match sets the bound value to null; the polyline's own
                //linetype is the only sensible reading of "no linetype chosen".
                Linetype = IsPolylineLinetypeChoice(Linetype)
                    ? SegmentStyle.PolylineLinetype
                    : Linetype.Trim(),
            };

        /// <summary>True when <paramref name="name"/> is the placeholder or nothing at all.</summary>
        public static bool IsPolylineLinetypeChoice(string? name) =>
            string.IsNullOrWhiteSpace(name) ||
            string.Equals(name.Trim(), PolylineLinetypeChoice, StringComparison.Ordinal);

        protected override void OnFixedColorPicked() => ColorMode = SegmentColorMode.FixedColor;

        partial void OnColorModeChanged(SegmentColorMode value) =>
            OnPropertyChanged(nameof(IsFixedColor));
    }
}
