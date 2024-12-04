using DimensioneringV2.GraphFeatures;

using Mapsui;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    class StyleDefault : StyleBase
    {
        public StyleDefault(IEnumerable<IFeature> features) : base(features)
        {
        }
    }
}
