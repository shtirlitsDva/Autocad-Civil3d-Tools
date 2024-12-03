using DimensioneringV2.GraphFeatures;

using Mapsui;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    class StyleCalculatedNumberOfBuildingsSupplied
    {
        private IEnumerable<AnalysisFeature> _features;
        public StyleCalculatedNumberOfBuildingsSupplied(IEnumerable<AnalysisFeature> features)
        {
            _features = features;
        }


    }
}
