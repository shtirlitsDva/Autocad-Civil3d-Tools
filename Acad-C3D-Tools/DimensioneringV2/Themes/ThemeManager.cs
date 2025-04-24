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

        public ThemeManager(IEnumerable<AnalysisFeature> allFeatures)
        {
            _allFeatures = allFeatures;
            RegisterCategoryThemes();
        }

        private readonly Dictionary<MapPropertyEnum, Func<IStyle>> _categoryThemeBuilders = new();

        private void RegisterCategoryThemes()
        {
            _categoryThemeBuilders[MapPropertyEnum.SubGraphId] =
                () => BuildCategoryTheme(MapPropertyEnum.SubGraphId, f => f.SubGraphId);

            _categoryThemeBuilders[MapPropertyEnum.CriticalPath] =
                () => BuildCategoryTheme(MapPropertyEnum.CriticalPath, f => f.IsCriticalPath);

            _categoryThemeBuilders[MapPropertyEnum.Bridge] =
                () => BuildCategoryTheme(MapPropertyEnum.Bridge, f => f.IsBridge);

            _categoryThemeBuilders[MapPropertyEnum.Pipe] =
                () => BuildCategoryTheme(MapPropertyEnum.Pipe, f => f.PipeDim.DimName);
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

            double min = values.Min();
            double max = values.Max();

            var theme = new GradientTheme(
                columnName: AnalysisFeature.GetAttributeName(prop),
                minValue: min,
                maxValue: max,
                minStyle: new VectorStyle { Line = new Pen(Color.Green, 2) },
                maxStyle: new VectorStyle { Line = new Pen(Color.Red, 8) }
            );

            theme.LineColorBlend = ColorBlend.Rainbow5;
            return theme;
        }

        private IStyle BuildCategoryTheme<T>(
            MapPropertyEnum prop,
            Func<AnalysisFeature, T>? selector = null)
        {
            var key = AnalysisFeature.GetAttributeName(prop);
            selector ??= f => (T)f[prop]!;

            // Gather distinct category values
            var values = _allFeatures
                .Cast<AnalysisFeature>()
                .Select(selector)
                .Distinct()
                .OrderBy(x => x) // Ensure consistent ordering
                .ToList();

            if (values.Count == 0) return new DefaultTheme();

            var colorBlend = ColorBlend.Rainbow7;
            //workaround for the Rainbow7 bug
            colorBlend.Positions![^1] = 1.0;
            int count = values.Count;
            var styles = new Dictionary<T, IStyle>(count);

            for (int i = 0; i < count; i++)
            {
                float pos = count == 1 ? 0.5f : (float)i / (count - 1);
                var color = colorBlend.GetColor(pos);
                styles[values[i]] = new VectorStyle
                {
                    Line = new Pen(color, 4)                    
                };                
            }

            return new CategoryTheme<T>(selector, styles);
        }
    }
}