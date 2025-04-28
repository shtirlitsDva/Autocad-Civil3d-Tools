using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Legend;

using Mapsui;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Themes
{
    class CategoryTheme<T> : StyleBase, IThemeStyle, IStyle, ILegendItemProvider
    {
        private Func<AnalysisFeature, T> _valueSelector { get; }

        private IDictionary<T, IStyle> _stylesMap;

        private IList<LegendItem> _legendItems;

        public CategoryTheme(
            Func<AnalysisFeature, T> valueSelector,
            IDictionary<T, IStyle> stylesMap,
            IList<LegendItem> legendItems)
        {
            _valueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));
            _stylesMap = stylesMap ?? throw new ArgumentNullException(nameof(stylesMap));
            _legendItems = legendItems ?? throw new ArgumentNullException(nameof(legendItems));
        }

        public IStyle? GetStyle(IFeature feature)
        {
            if (feature == null) throw new ArgumentNullException("Passed feature is null!");

            var afeature = feature as AnalysisFeature;
            if (afeature == null) return StyleProvider.BasicStyle;

            T key = _valueSelector(afeature);
            if (_stylesMap.TryGetValue(key, out var style)) return style;
            return StyleProvider.BasicStyle;
        }

        public IList<LegendItem> GetLegendItems() => _legendItems;
    }
}