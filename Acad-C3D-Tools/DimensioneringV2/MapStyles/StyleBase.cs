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
    internal abstract class StyleBase : IStyleManager
    {
        private IStyle _style = new VectorStyle();
        protected IEnumerable<IFeature> _features;
        protected StyleBase(IEnumerable<IFeature> features) { _features = features; }
        public IEnumerable<IFeature> ApplyStyle()
        {
            foreach (var feature in _features)
            {
                feature.Styles.Clear();
                var ss = GetStyles(feature);
                for (var i = 0; i < ss.Length; i++)
                {
                    feature.Styles.Add(ss[i]);
                }
            }

            return _features;
        }
        public virtual IStyle[] GetStyles(IFeature feature)
        {
            return [_style];
        }

    }
}
