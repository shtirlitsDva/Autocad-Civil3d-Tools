using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using DimensioneringV2.GraphFeatures;

using MathNet.Numerics.Statistics;

using NetTopologySuite.Geometries;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

namespace DimensioneringV2.Serialization
{
    internal class AnalysisFeatureDto
    {
        public double[][] Coordinates { get; set; }
        public double[][]? OriginalStik { get; set; }
        public double[][]? OriginalVej { get; set; }

        public Dictionary<string, JsonElement> Attributes { get; set; }

        public AnalysisFeatureDto() { }

        public AnalysisFeatureDto(AnalysisFeature analysisFeature)
        {
            if (analysisFeature.Geometry is not LineString line)
                throw new ArgumentException("Expected LineString geometry");

            Coordinates = line.Coordinates
                .Select(c => new[] { c.X, c.Y })
                .ToArray();

            if (analysisFeature.OriginalGeometry.Stik != null)
                OriginalStik = 
                    analysisFeature.OriginalGeometry.Stik.Coordinates
                    .Select(c => new[] {c.X, c.Y})
                    .ToArray();

            if (analysisFeature.OriginalGeometry.Vej != null)
                OriginalVej =
                    analysisFeature.OriginalGeometry.Vej.Coordinates
                    .Select(c => new[] { c.X, c.Y })
                    .ToArray();

            Attributes = new Dictionary<string, JsonElement>();

            foreach (var field in analysisFeature.Fields)
            {
                var value = analysisFeature[field];
                Attributes[field] = JsonSerializer.SerializeToElement(value);
            }
        }

        public AnalysisFeature ToAnalysisFeature(JsonSerializerOptions? options = null)
        {
            var geometry = new LineString(Coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray());
            LineString? originalStik = null;
            if (OriginalStik != null)
                originalStik = new LineString(
                    OriginalStik.Select(
                        c => new Coordinate(c[0], c[1])).ToArray());
            LineString? originalVej = null;
            if (OriginalVej != null)
                originalVej = new LineString(
                    OriginalVej.Select(
                        c => new Coordinate(c[0], c[1])).ToArray());
            var typedAttributes = DictionaryObjectConverter.ConvertAttributesToTyped(
                Attributes, typeof(AnalysisFeature), options);
            return new AnalysisFeature(
                geometry, new OriginalGeometry(originalStik, originalVej), typedAttributes);
        }
    }
}