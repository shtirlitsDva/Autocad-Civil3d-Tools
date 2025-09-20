using Autodesk.Windows;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    internal class PenWidthCalculator<T> where T : struct, IComparable
    {
        private double _minPenWidth;
        private double _maxPenWidth;
        private double _scaleFactor;
        private double _offset;
        public PenWidthCalculator(double minPenWidth = 2, double maxPenWidth = 10)
        {
            _maxPenWidth = maxPenWidth;
            _minPenWidth = minPenWidth;
        }
        public void SetMinMaxValues(T min, T max)
        {
            dynamic minValue = min;
            dynamic maxValue = max;

            _scaleFactor = (_maxPenWidth - _minPenWidth) / (maxValue - minValue);
            _offset = _minPenWidth - minValue * _scaleFactor;
        }
        public double CalculatePenWidth(T value)
        {
            dynamic val = value;
            return _scaleFactor * val + _offset;
        }
    }
}
