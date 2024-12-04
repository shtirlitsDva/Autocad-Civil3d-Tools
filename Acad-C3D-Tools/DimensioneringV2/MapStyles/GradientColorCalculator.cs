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
        private Dictionary<int, Color> _gradientLookup;
        private int[] _boundaries;

        public GradientHelper(int minValue, int maxValue)
        {
            _boundaries = PrecalculateBoundaries(minValue, maxValue, 10);
            _gradientLookup = CreateGradientLookup(_boundaries);
        }

        private int[] PrecalculateBoundaries(int minValue, int maxValue, int binCount)
        {
            int[] boundaries = new int[binCount + 1];
            double range = maxValue - minValue;
            double binSize = range / binCount;

            for (int i = 0; i <= binCount; i++)
            {
                boundaries[i] = (int)(minValue + i * binSize);
            }

            return boundaries;
        }

        private Dictionary<int, Color> CreateGradientLookup(int[] boundaries)
        {
            var lookupTable = new Dictionary<int, Color>();
            int binCount = boundaries.Length - 1;

            for (int i = 0; i < binCount; i++)
            {
                Color color = GetGradientColor((double)i / (binCount - 1));
                lookupTable[i] = color;
            }

            return lookupTable;
        }

        private Color GetGradientColor(double ratio)
        {
            // Ensure ratio is in the range [0, 1]
            ratio = Math.Max(0, Math.Min(1, ratio));

            byte red = (byte)(ratio * 255);
            byte green = 0;
            byte blue = (byte)((1 - ratio) * 255);

            return new Color(red, green, blue, 255); // RGBA format with full opacity
        }

        public Color LookupColor(int value)
        {
            switch (value)
            {
                case int v when v >= _boundaries[0] && v < _boundaries[1]:
                    return _gradientLookup[0];
                case int v when v >= _boundaries[1] && v < _boundaries[2]:
                    return _gradientLookup[1];
                case int v when v >= _boundaries[2] && v < _boundaries[3]:
                    return _gradientLookup[2];
                case int v when v >= _boundaries[3] && v < _boundaries[4]:
                    return _gradientLookup[3];
                case int v when v >= _boundaries[4] && v < _boundaries[5]:
                    return _gradientLookup[4];
                case int v when v >= _boundaries[5] && v < _boundaries[6]:
                    return _gradientLookup[5];
                case int v when v >= _boundaries[6] && v < _boundaries[7]:
                    return _gradientLookup[6];
                case int v when v >= _boundaries[7] && v < _boundaries[8]:
                    return _gradientLookup[7];
                case int v when v >= _boundaries[8] && v < _boundaries[9]:
                    return _gradientLookup[8];
                default:
                    return _gradientLookup[9];
            }
        }
    }
}
