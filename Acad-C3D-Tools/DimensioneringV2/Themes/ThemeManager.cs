using DimensioneringV2.UI;
using DimensioneringV2.GraphFeatures;
using Mapsui.Styles.Thematics;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DimensioneringV2.Legend;
using Mapsui.Extensions;
using DimensioneringV2.Labels;

namespace DimensioneringV2.Themes
{
    class ThemeManager
    {
        private readonly IEnumerable<AnalysisFeature> _allFeatures;
        public IStyle? CurrentTheme { get; private set; }
        private ColorBlend _cb = ColorBlendProvider.Standard;

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

            _categoryThemeBuilders[MapPropertyEnum.ManualDim] =
                () => BuildCategoryTheme(
                    MapPropertyEnum.ManualDim, f => f.ManualDim, [false]);

            _categoryThemeBuilders[MapPropertyEnum.Bridge] =
                () => BuildCategoryTheme(
                    MapPropertyEnum.Bridge, f => f.IsBridge, [true]);

            _categoryThemeBuilders[MapPropertyEnum.Pipe] =
                () => BuildCategoryTheme(
                    MapPropertyEnum.Pipe, f => f.Dim.DimName, ["NA 000"]);
        }

        public void SetTheme(MapPropertyEnum property, bool labelsEnabled = false)
        {
            IStyle theme = null;

            switch (property)
            {
                case MapPropertyEnum.DimFlowSupply:
                case MapPropertyEnum.DimFlowReturn:
                case MapPropertyEnum.PressureGradientSupply:
                case MapPropertyEnum.PressureGradientReturn:
                case MapPropertyEnum.VelocitySupply:
                case MapPropertyEnum.VelocityReturn:
                case MapPropertyEnum.UtilizationRate:
                case MapPropertyEnum.HeatingDemand:
                case MapPropertyEnum.Bygninger:
                case MapPropertyEnum.Units:
                case MapPropertyEnum.TempDeltaVarme:
                case MapPropertyEnum.TempDeltaBV:
                    theme = BuildGradientTheme(property);
                    break;

                case MapPropertyEnum.SubGraphId:
                case MapPropertyEnum.CriticalPath:
                case MapPropertyEnum.ManualDim:
                case MapPropertyEnum.Bridge:
                case MapPropertyEnum.Pipe:
                    theme = _categoryThemeBuilders[property]();
                    break;

                // Fallback or default
                default:
                    CurrentTheme = new StyleCollection() { Styles = [new DefaultTheme()] };
                    return;
            }

            if (labelsEnabled && property != MapPropertyEnum.Default)
            {
                CurrentTheme = new StyleCollection()
                {
                    Styles = [theme, LabelManager.GetLabelStyle(property)]
                };
            }
            else
            {
                CurrentTheme = theme;
            }
        }
        public ILegendData? GetTheme()
        {
            if (CurrentTheme is StyleCollection col) return col.Styles[0] as ILegendData;
            else return CurrentTheme as ILegendData;
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
            var query = values.Where(x => x > 1e-9);
            double min = query.Count() > 0 ? query.Min() : 0;
            double max = query.Count() > 0 ? values.Max() : 1;

            var theme = new GradientWithDefaultTheme(
                columnName: AnalysisFeature.GetAttributeName(prop),
                minValue: min,
                maxValue: max,
                minStyle: new VectorStyle { Line = new Pen(Color.Blue, 2) },
                maxStyle: new VectorStyle { Line = new Pen(Color.Red, 10) },
                legendTitle: LegendTitleProvider.GetTitle(prop)
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

            //Create legend items
            var legendItems = new List<LegendItem>(count);

            //Assign basic style to values that we don't want to style
            foreach (var value in basicStyleValues)
            {
                styles[value] = StyleProvider.BasicStyle;
                var li = new LegendItem()
                {
                    Label = LegendLabelProvider.GetLabel(prop, value),
                    SymbolColor = Color.Black,
                    SymbolLineWidth = 1.5f
                };
                legendItems.Add(li);
            }

            //Assign colors to the rest of the values
            for (int i = 0; i < count; i++)
            {
                double pos = count == 1 ? 1 : (double)i / (count - 1);
                var color = colorBlend.GetColor(pos);
                styles[values[i]] = new VectorStyle
                {
                    Line = new Pen(color, 4)
                };

                var li = new LegendItem()
                {
                    Label = LegendLabelProvider.GetLabel(prop, values[i]),
                    SymbolColor = color,
                    SymbolLineWidth = 4
                };
                legendItems.Add(li);
            }

            return new CategoryTheme<T>(
                selector, styles, legendItems,
                LegendTitleProvider.GetTitle(prop));
        }
    }
}