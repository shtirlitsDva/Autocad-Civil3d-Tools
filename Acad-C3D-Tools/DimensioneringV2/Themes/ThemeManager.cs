using DimensioneringV2.UI;
using DimensioneringV2.GraphFeatures;
using Mapsui.Styles.Thematics;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Themes
{
    class ThemeManager
    {
        private readonly IEnumerable<AnalysisFeature> _allFeatures;
        public IStyle? CurrentTheme { get; private set; }
        private ColorBlend _cb = GetColorBlend(
            [Color.Blue, Color.Yellow, Color.Red]
        );

        public ThemeManager(IEnumerable<AnalysisFeature> allFeatures)
        {
            _allFeatures = allFeatures;
            RegisterCategoryThemes();
        }

        private readonly Dictionary<MapPropertyEnum, Func<IStyle>> _categoryThemeBuilders = new();

        private void RegisterCategoryThemes()
        {
            _categoryThemeBuilders[MapPropertyEnum.SubGraphId] =
                () => BuildCategoryTheme(
                    MapPropertyEnum.SubGraphId, f => f.SubGraphId, []);

            _categoryThemeBuilders[MapPropertyEnum.CriticalPath] =
                () => BuildCategoryTheme(
                    MapPropertyEnum.CriticalPath, f => f.IsCriticalPath, [false]);

            _categoryThemeBuilders[MapPropertyEnum.Bridge] =
                () => BuildCategoryTheme(
                    MapPropertyEnum.Bridge, f => f.IsBridge, [true]);

            _categoryThemeBuilders[MapPropertyEnum.Pipe] =
                () => BuildCategoryTheme(
                    MapPropertyEnum.Pipe, f => f.PipeDim.DimName, ["NA 000"]);
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
                case MapPropertyEnum.Bygninger:
                case MapPropertyEnum.Units:
                    CurrentTheme = BuildGradientTheme(property);
                    break;

                case MapPropertyEnum.SubGraphId:
                case MapPropertyEnum.CriticalPath:
                case MapPropertyEnum.Bridge:
                case MapPropertyEnum.Pipe:
                    CurrentTheme = _categoryThemeBuilders[property]();
                    break;

                // Fallback or default
                default:
                    CurrentTheme = new DefaultTheme();
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

            if (!values.Any()) return new DefaultTheme();

            //Handle zero values, the idea is that Min is larger than zero
            //If a zero value is passed to the Theme, it returns basic style            
            double min = values.Min();
            if (min == 0.0) { values.Remove(min); min = values.Min(); }

            double max = values.Max();

            var theme = new GradientWithDefaultTheme(
                columnName: AnalysisFeature.GetAttributeName(prop),
                minValue: min,
                maxValue: max,
                minStyle: new VectorStyle { Line = new Pen(Color.Blue, 2) },
                maxStyle: new VectorStyle { Line = new Pen(Color.Red, 10) }
            )
            {
                LineColorBlend = _cb
            };

            return theme;
        }

        private IStyle BuildCategoryTheme<T>(
            MapPropertyEnum prop,
            Func<AnalysisFeature, T> selector,
            T[] basicStyleValues)
        {
            // Gather distinct category values
            var values = ValueListProvider.GetValues(
                prop, _allFeatures.Cast<AnalysisFeature>(), selector, basicStyleValues);

            if (values.Count == 0) return new DefaultTheme();

            var colorBlend = _cb;

            int count = values.Count;
            var styles = new Dictionary<T, IStyle>(count);

            //Assign basic style to values that we don't want to style
            foreach (var value in basicStyleValues)
            {
                styles[value] = StyleProvider.BasicStyle;
            }

            //Assign colors to the rest of the values
            for (int i = 0; i < count; i++)
            {
                double pos = count == 1 ? 0.5 : (double)i / (count - 1);
                var color = colorBlend.GetColor(pos);
                styles[values[i]] = new VectorStyle
                {
                    Line = new Pen(color, 4)
                };
            }

            return new CategoryTheme<T>(selector, styles);
        }

        private static ColorBlend GetColorBlend(Color[] colors)
        {
            var positions = Enumerable.Range(0, colors.Length).Select(i => (double)i / (colors.Length - 1)).ToArray();
            return new ColorBlend(colors, positions);
        }
    }
}