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
    internal abstract class StyleBase : IStyleManager
    {
        public virtual IStyle GetStyle(IFeature feature)
        {
            return new VectorStyle();
        }
    }
}
