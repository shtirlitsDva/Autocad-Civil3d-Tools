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
    internal interface IStyleManager
    {
        IStyle[] GetStyles(IFeature feature);
        IEnumerable<IFeature> ApplyStyle();
    }
}
