using DimensioneringV2.MapStyles;

using Mapsui;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class FeatureStyleService
    {
        internal static IEnumerable<IFeature> ApplyStyle(IEnumerable<IFeature> features, IStyleManager styleManager)
        {
            foreach (IFeature feature in features)
            {
                feature.Styles.Add(styleManager.GetStyle(feature));

                yield return feature;
            }
        }
    }
}
