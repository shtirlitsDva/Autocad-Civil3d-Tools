using DimensioneringV2.UI;
using DimensioneringV2.UI.MapProperty;
using DimensioneringV2.GraphFeatures;
using Mapsui.Styles.Thematics;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using DimensioneringV2.Legend;
using Mapsui.Extensions;
using DimensioneringV2.Labels;

namespace DimensioneringV2.Themes
{
    class ThemeManager
    {
        private readonly IEnumerable<AnalysisFeature> _allFeatures;
        public IStyle? CurrentTheme { get; private set; }
        private OklchGradient _cb = ColorBlendProvider.Standard;

        public ThemeManager(IEnumerable<AnalysisFeature> allFeatures)
        {
            _allFeatures = allFeatures;
        }

        public void SetTheme(MapPropertyEnum property, bool labelsEnabled = false)
        {
            if (property == MapPropertyEnum.Default || property == MapPropertyEnum.Basic)
            {
                CurrentTheme = new StyleCollection() { Styles = [new DefaultTheme()] };
                return;
            }

            if (!MapPropertyMetadata.TryGet(property, out var meta) || meta.ThemeKind == ThemeKind.None)
            {
                CurrentTheme = new StyleCollection() { Styles = [new DefaultTheme()] };
                return;
            }

            IStyle theme = meta.ThemeKind switch
            {
                ThemeKind.Gradient => BuildGradientTheme(meta),
                ThemeKind.Category => BuildCategoryTheme(meta),
                _ => new DefaultTheme()
            };

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

        public LegendElement? GetLegendContent()
        {
            ILegendSource? source = CurrentTheme is StyleCollection col
                ? col.Styles[0] as ILegendSource
                : CurrentTheme as ILegendSource;
            return source?.BuildLegendPanel();
        }

        private IStyle BuildGradientTheme(PropertyMeta meta)
        {
            // Use GetDisplayValue to invoke property getter with any custom logic
            var values = _allFeatures
                .Select(f => Convert.ToDouble(f.GetDisplayValue(meta.Enum)))
                .Where(v => !double.IsNaN(v))
                .ToList();

            if (!values.Any()) return new DefaultTheme();

            //Handle zero values, the idea is that Min is larger than zero
            //If a zero value is passed to the Theme, it returns basic style
            var query = values.Where(x => x > 1e-9);
            double min = query.Count() > 0 ? query.Min() : 0;
            double max = query.Count() > 0 ? values.Max() : 1;

            var theme = new GradientWithDefaultTheme(
                property: meta.Enum,
                minValue: min,
                maxValue: max,
                minStyle: new VectorStyle { Line = new Pen(Color.Blue, 2) },
                maxStyle: new VectorStyle { Line = new Pen(Color.Red, 10) },
                legendTitle: meta.LegendTitle
            )
            {
                LineColorBlend = _cb
            };

            return theme;
        }

        private IStyle BuildCategoryTheme(PropertyMeta meta)
        {
            var basicStyleValues = meta.GetTypedBasicStyleValues();
            Func<AnalysisFeature, object?> selector = f => meta.ResolveDisplayValue(f);

            var values = ValueListProvider.GetValues(
                meta, _allFeatures, selector, basicStyleValues);

            if (values.Count == 0) return new DefaultTheme();

            var colorBlend = _cb;

            int count = values.Count;
            var styles = new Dictionary<object, IStyle>(count, ObjectKeyComparer.Instance);

            //Create legend items
            var legendItems = new List<LegendItem>(count);

            //Assign basic style to values that we don't want to style
            foreach (var value in basicStyleValues)
            {
                styles[value] = StyleProvider.BasicStyle;
                var li = new LegendItem()
                {
                    Label = LegendLabelProvider.GetLabel(meta, value),
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
                    Label = LegendLabelProvider.GetLabel(meta, values[i]),
                    SymbolColor = color,
                    SymbolLineWidth = 4
                };
                legendItems.Add(li);
            }

            return new CategoryTheme(
                selector, styles, legendItems, meta.LegendTitle);
        }
    }
}
