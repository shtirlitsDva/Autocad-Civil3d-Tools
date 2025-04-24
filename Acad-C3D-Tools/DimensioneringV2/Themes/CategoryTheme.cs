using Autodesk.Gis.Map.DisplayManagement;

using DimensioneringV2.GraphFeatures;

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
    class CategoryTheme<T> : StyleBase, IThemeStyle, IStyle
    {
        private Func<AnalysisFeature, T> _valueSelector { get; }

        private IDictionary<T, IStyle> _stylesMap;

        private readonly IStyle _default = new VectorStyle
        {
            Line = new Pen(Color.Black) { Width = 2 }
        };

        public CategoryTheme(
            Func<AnalysisFeature, T> valueSelector,
            IDictionary<T, IStyle> stylesMap)
        {
            _valueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));
            _stylesMap = stylesMap ?? throw new ArgumentNullException(nameof(stylesMap));            
        }

        public IStyle? GetStyle(IFeature feature)
        {
            if (feature == null) throw new ArgumentNullException("Passed feature is null!");

            var afeature = feature as AnalysisFeature;
            if (afeature == null) return _default;

            T key = _valueSelector(afeature);
            if (_stylesMap.TryGetValue(key, out var style)) return style;
            return _default;
        }
    }
}
