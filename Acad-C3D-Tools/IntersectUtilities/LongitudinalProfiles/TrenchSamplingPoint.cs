using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities
{
    internal struct TrenchSamplingPoint
    {
        public double Depth { get => _terrænKote - _bundKote + 0.1; }
        public double Volume { get => Depth * _width * 0.1; }
        public double Width { get => _width; }
        public double StepLength { get => _stepLength; }
        public string Key { get; }
        private double _terrænKote;
        private double _bundKote;
        private double _width;
        private double _stepLength;
        public TrenchSamplingPoint(double terrænKote, double bundKote, double width, double stepLength, string key)
        {
            _terrænKote = terrænKote; _bundKote = bundKote; _width = width; _stepLength = stepLength; Key = key;
        }
    }
}
