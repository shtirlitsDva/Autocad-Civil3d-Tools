using DimensioneringV2.GraphFeatures;

using DotSpatial.Projections;

using Mapsui;
using Mapsui.Extensions;
using Mapsui.Extensions.Projections;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.UI.Objects;

using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class ProjectionService
    {
        internal static IEnumerable<AnalysisFeature> ReProjectFeatures(
            IEnumerable<AnalysisFeature> features, string sourceCrs, string targetCrs) =>
                features
            .Cast<IFeature>()
            .Project(sourceCrs, targetCrs, new DotSpatialProjection())
            .Cast<AnalysisFeature>();
    }
}
