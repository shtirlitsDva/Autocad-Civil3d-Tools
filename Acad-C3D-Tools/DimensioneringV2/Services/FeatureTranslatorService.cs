using Mapsui.Nts;

using NetTopologySuite.Features;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class FeatureTranslatorService
    {
        internal static List<GeometryFeature> TranslateNtsFeatures(IEnumerable<IFeature> ntsFeatures)
        {
            List<GeometryFeature> geometryFeatures = new();

            foreach (IFeature feature in ntsFeatures)
            {
                GeometryFeature geometryFeature = new()
                {
                    Geometry = feature.Geometry,
                };

                foreach (string key in feature.Attributes.GetNames())
                {
                    geometryFeature[key] = feature.Attributes[key];
                }

                geometryFeatures.Add(geometryFeature);
            }
            return geometryFeatures;
        }
    }
}
