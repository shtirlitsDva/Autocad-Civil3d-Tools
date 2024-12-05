using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    internal class GradientHelper
    {
        private int _minValue;
        private int _maxValue;

        public GradientHelper(int minValue, int maxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue;
        }
        public Color GetGradientColor(int value)
        {
            // Ensure value is within bounds
            value = Math.Max(_minValue, Math.Min(value, _maxValue));

            // Calculate the ratio between min and max values
            double ratio = (double)(value - _minValue) / (_maxValue - _minValue);

            // Use HSL to RGB conversion to generate a brighter gradient color
            // Hue ranges from 240 (blue) to 0 (red), keeping saturation and lightness high for bright colors
            double hue = 240 - (240 * ratio);
            double saturation = 0.9; // High saturation for vivid colors
            double lightness = 0.6; // Higher lightness for brighter colors

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
