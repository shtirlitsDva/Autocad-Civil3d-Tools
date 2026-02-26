using System;
using System.Collections.Generic;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI;

using Mapsui;
using Mapsui.Styles;

namespace DimensioneringV2.Labels
{
    class LabelManager
    {
        // Escape hatch: custom formatters for properties with genuinely complex logic
        private static readonly Dictionary<MapPropertyEnum, Func<AnalysisFeature, string?>>
            _customFormatters = new()
        {
            [MapPropertyEnum.CriticalPath] = af =>
            {
                if (af.SegmentType != NorsynHydraulicCalc.SegmentType.Stikledning) return null;
                return af.DifferentialPressureAtClient.ToString("F2");
            }
        };

        public static LabelStyle? GetLabelStyle(MapPropertyEnum prop)
        {
            if (prop == MapPropertyEnum.Default) return null;

            if (!MapPropertyMetadata.TryGet(prop, out var meta))
                return null;

            var labelStyle = new LabelStyle
            {
                LabelMethod = (feature) =>
                {
                    if (feature is not AnalysisFeature af) return null;

                    // Check for custom formatter first (escape hatch)
                    if (meta.LabelFormat == LabelFormat.Custom
                        && _customFormatters.TryGetValue(prop, out var custom))
                    {
                        return custom(af);
                    }

                    var value = af.GetDisplayValue(prop);

                    return meta.LabelFormat switch
                    {
                        LabelFormat.Percentage => value is double d
                            ? $"{d * 100:F0}%"
                            : value?.ToString(),
                        LabelFormat.HideIfZeroOrLess => value switch
                        {
                            int i when i <= 0 => null,
                            double d when d <= 0 => null,
                            _ => value?.ToString()
                        },
                        _ => DefaultFormat(value)
                    };
                },

                ForeColor = Color.Black,
                Font = new Font() { FontFamily = "Arial", Size = 10 },
                BackColor = new Brush() { Color = Color.White, FillStyle = FillStyle.Solid },
                Halo = new Pen() { Color = Color.White, Width = 2 },
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
                CollisionDetection = true,
            };

            return labelStyle;
        }

        private static string? DefaultFormat(object? value) => value switch
        {
            double d => d.ToString("F2"),
            float f => f.ToString("F2"),
            int i => i.ToString(),
            string s => s,
            _ => value?.ToString()
        };
    }
}
