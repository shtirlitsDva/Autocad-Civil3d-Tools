using Autodesk.Windows;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    internal class PenWidthCalculator
    {
        private double _minPenWidth;
        private double _maxPenWidth;
        private double _min;
        private double _max;
        private double _scaleFactor;
        private double _offset;
        public PenWidthCalculator(double minPenWidth = 2, double maxPenWidth = 10)
        {
            _maxPenWidth = maxPenWidth;
            _minPenWidth = minPenWidth;
        }
        public void SetMinMaxValues(int min, int max)
        {
            _min = min;
            _max = max;
            _scaleFactor = (_maxPenWidth - _minPenWidth) / (_max - _min);
            _offset = _minPenWidth - min * _scaleFactor;
        }
        public double CalculatePenWidth(int value)
        {
            return _scaleFactor * value + _offset;
        }
    }
}
