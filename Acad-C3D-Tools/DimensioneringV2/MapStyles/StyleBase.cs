using DimensioneringV2.GraphFeatures;

using Mapsui;
using Mapsui.Extensions;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    internal abstract class StyleBase : IMapStyle
    {
        private IStyle _style = new VectorStyle() { Line = new Pen(Color.Black, 0.5), Opacity = 50f  };
        protected StyleBase() { }
        public virtual void ApplyStyle(IEnumerable<IFeature> features)
        {
            foreach (var feature in features)
            {
                feature.Styles.Clear();
                var ss = GetStyles(feature);
                for (var i = 0; i < ss.Length; i++)
                {
                    feature.Styles.Add(ss[i]);
                }
            }
        }
        public virtual IStyle[] GetStyles(IFeature feature)
        {
            return [_style];
        }

    }
}
