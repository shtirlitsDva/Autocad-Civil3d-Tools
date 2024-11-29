using DimensioneringV2.GraphFeatures;
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
        private readonly ICoordinateTransformation _transformation;

        public ProjectionService()
        {
            // Define the UTM32N coordinate system and the target map projection (Spherical Mercator)
            var utm32N = ProjectedCoordinateSystem.WGS84_UTM(32, true); // True for Northern Hemisphere
            var sphericalMercator = ProjectedCoordinateSystem.WebMercator;

            // Create the coordinate transformation from UTM32N to WebMercator
            var coordinateTransformationFactory = new CoordinateTransformationFactory();
            _transformation = coordinateTransformationFactory.CreateFromCoordinateSystems(utm32N, sphericalMercator);
        }

        public ILayer CreateProjectedLayer(IEnumerable<FeatureNode> features)
        {
            // Create a feature provider for the original features in UTM32N
            var memoryProvider = new MemoryProvider(features)
            {
                CRS = ProjectedCoordinateSystem.WGS84_UTM(32, true).ToString(),
            };

            // Wrap the feature provider in a ProjectingProvider to automatically reproject coordinates
            var dataSource = new ProjectingProvider(memoryProvider)
            {
                CRS = "EPSG:3857"
            };

            // Create and return a new layer using the projecting provider
            return new MemoryLayer
            {
                DataSource = projectingProvider,
                Name = "Projected Layer"
            };
        }
    }
}
