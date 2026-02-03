using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI;

using Mapsui;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;

using System;

namespace DimensioneringV2.Themes
{
    internal class BBRTheme : StyleBase, IThemeStyle
    {
        private readonly bool _isActive;
        private readonly double _desiredWorldSize;
        private readonly Func<double> _getResolution;
        private const double BitmapSize = 48.0;

        public BBRTheme(bool isActive, Func<double> getResolution, double desiredWorldSize = 10.0)
        {
            _isActive = isActive;
            _getResolution = getResolution;
            _desiredWorldSize = desiredWorldSize;
        }

        private static bool _loggedOnce = false;

        public IStyle? GetStyle(IFeature feature)
        {
            if (feature is not BBRMapFeature bbr)
            {
                if (!_loggedOnce)
                {
                    DimensioneringV2.Utils.prtDbg($"BBRTheme: Feature is not BBRMapFeature, it's {feature?.GetType().Name ?? "null"}");
                    _loggedOnce = true;
                }
                return null;
            }

            var bitmapId = _isActive
                ? MapSuiSvgIconCache.GetBitmapId(bbr.HeatingType)
                : MapSuiSvgIconCache.GetInactiveBitmapId(bbr.HeatingType);

            if (bitmapId < 0)
                return null;

            var resolution = _getResolution();
            var symbolScale = _desiredWorldSize / (resolution * BitmapSize);

            if (!_loggedOnce)
            {
                DimensioneringV2.Utils.prtDbg($"BBRTheme: Resolution={resolution:F2}, Scale={symbolScale:F4}, WorldSize={_desiredWorldSize}");
                _loggedOnce = true;
            }

            return new SymbolStyle
            {
                BitmapId = bitmapId,
                SymbolScale = symbolScale,
                SymbolOffset = new Offset(0, 0)
            };
        }
    }
}
