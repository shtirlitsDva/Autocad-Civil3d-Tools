using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    internal class GradientHelper<T> where T : struct, IComparable
    {
        private T _minValue;
        private T _maxValue;

        public GradientHelper(T minValue, T maxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue;
        }
        public Color GetGradientColor(T value)
        {
            // Normalize value
            dynamic min = _minValue;
            dynamic max = _maxValue;
            dynamic val = value;

            double ratio = (double)(val - min) / (max - min);

            // Generate gradient color
            double hue = 240 - (240 * ratio);
            double saturation = 0.9;
            double lightness = 0.6;

            return HslToRgb(hue, saturation, lightness);
        }

        private Color HslToRgb(double hue, double saturation, double lightness)
        {
            double c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = lightness - c / 2;

            double r = 0, g = 0, b = 0;

            switch ((int)(hue / 60))
            {
                case 0:
                    r = c; g = x; b = 0;
                    break;
                case 1:
                    r = x; g = c; b = 0;
                    break;
                case 2:
                    r = 0; g = c; b = x;
                    break;
                case 3:
                    r = 0; g = x; b = c;
                    break;
                case 4:
                    r = x; g = 0; b = c;
                    break;
                case 5:
                    r = c; g = 0; b = x;
                    break;
            }

            byte red = (byte)((r + m) * 255);
            byte green = (byte)((g + m) * 255);
            byte blue = (byte)((b + m) * 255);

            return new Color(red, green, blue, 255); // RGBA format with full opacity
        }
    }
}
