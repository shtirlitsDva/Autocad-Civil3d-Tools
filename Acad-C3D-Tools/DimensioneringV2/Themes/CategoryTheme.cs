using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Legend;

using Mapsui;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;

using System;
using System.Collections.Generic;

namespace DimensioneringV2.Themes
{
    class CategoryTheme : StyleBase, IThemeStyle, IStyle, ILegendSource
    {
        private Func<AnalysisFeature, object?> _valueSelector { get; }

        private string _legendTitle;
        private IDictionary<object, IStyle> _stylesMap;
        private IList<LegendItem> _legendItems;

        public CategoryTheme(
            Func<AnalysisFeature, object?> valueSelector,
            IDictionary<object, IStyle> stylesMap,
            IList<LegendItem> legendItems,
            string legendTitle)
        {
            _valueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));
            _stylesMap = stylesMap ?? throw new ArgumentNullException(nameof(stylesMap));
            _legendItems = legendItems ?? throw new ArgumentNullException(nameof(legendItems));
            _legendTitle = legendTitle;
        }

        public IStyle? GetStyle(IFeature feature)
        {
            if (feature == null) throw new ArgumentNullException("Passed feature is null!");

            var afeature = feature as AnalysisFeature;
            if (afeature == null) return StyleProvider.BasicStyle;

            var key = _valueSelector(afeature);
            if (key != null && _stylesMap.TryGetValue(key, out var style)) return style;
            return StyleProvider.BasicStyle;
        }

        public LegendElement? BuildLegendPanel() => new StackPanel
        {
            Background = new Color(255, 255, 255, 200),
            Padding = new Thickness(10, 5, 10, 5),
            Children = [
                new TextBlock
                {
                    Text = _legendTitle,
                    FontSize = 16,
                    Align = LegendTextAlign.Center,
                },
                new Spacer { Height = 4 },
                new ItemList { Items = _legendItems },
            ]
        };
    }
}
