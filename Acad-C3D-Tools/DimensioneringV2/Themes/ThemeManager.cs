using DimensioneringV2.MapStyles;
using DimensioneringV2.UI;
using Mapsui.Styles.Thematics;
using Mapsui.Styles;
using NetTopologySuite.Features;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DimensioneringV2.GraphFeatures;

namespace DimensioneringV2.Themes
{
    class ThemeManager
    {
        private readonly IEnumerable<AnalysisFeature> _allFeatures;
        public IStyle? CurrentTheme { get; private set; }

        public ThemeManager(IEnumerable<AnalysisFeature> allFeatures)
        {
            _allFeatures = allFeatures;
        }

        public void SetTheme(MapPropertyEnum property)
        {
            switch (property)
            {
                case MapPropertyEnum.FlowSupply:
                case MapPropertyEnum.FlowReturn:
                case MapPropertyEnum.PressureGradientSupply:
                case MapPropertyEnum.PressureGradientReturn:
                case MapPropertyEnum.VelocitySupply:
                case MapPropertyEnum.VelocityReturn:
                case MapPropertyEnum.UtilizationRate:
                case MapPropertyEnum.HeatingDemand:
                    CurrentTheme = BuildGradientTheme(property);
                    break;

                case MapPropertyEnum.Bygninger:
                case MapPropertyEnum.Units:
                case MapPropertyEnum.SubGraphId:
                    CurrentTheme = BuildCategoryTheme(property);
                    break;

                case MapPropertyEnum.Pipe:
                    CurrentTheme = BuildCategoryTheme("PipeDim");  // e.g. if your feature has "PipeSize"
                    break;

                // Fallback or default
                default:
                    CurrentTheme = new StyleDefault();
                    break;
            }
        }

        private IStyle BuildGradientTheme(MapPropertyEnum prop)
        {
            // Make sure each feature has feature[key] set to numeric
            var values = _allFeatures
                .Select(f => Convert.ToDouble(f[prop]))
                .Where(v => !double.IsNaN(v))
                .ToList();

            if (!values.Any()) return new StyleDefault();

            double min = values.Min();
            double max = values.Max();

            var theme = new GradientTheme(
                columnName: AnalysisFeature.GetAttributeName(prop),
                minValue: min,
                maxValue: max,
                minStyle: new VectorStyle { Line = new Pen(Color.Green, 1) },
                maxStyle: new VectorStyle { Line = new Pen(Color.Red, 4) }
            );

            theme.LineColorBlend = ColorBlend.Rainbow5;

            return theme;
        }

        private IStyle BuildCategoryTheme(MapPropertyEnum prop)
        {
            // Gather distinct category values
            var categories = _allFeatures.Select(f => f[prop]).Distinct().ToList();
            if (categories.Count == 0) return new StyleDefault();

            // Example color set; add more as needed
            var colorPool = new[] { Color.Green, Color.Blue, Color.Red, Color.Orange, Color.Purple };

            var themeDict = new Dictionary<object, IStyle>();
            int idx = 0;
            foreach (var cat in categories)
            {
                var color = colorPool[idx % colorPool.Length];
                themeDict[cat] = new VectorStyle
                {
                    Line = new Pen(color, 2 + (idx % 3)) // vary the line width a bit
                };
                idx++;
            }

            return new CategoryTheme(key) { Themes = themeDict };
        }
    }
}
