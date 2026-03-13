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
        /// <summary>
        /// Legacy: was EPSG:3857 display coordinates. Kept for backward-compat reads.
        /// New files write Geometry25832 only; Coordinates may be null on new saves.
        /// </summary>
        public double[][] Coordinates { get; set; }

        /// <summary>
        /// The authoritative geometry in EPSG:25832.
        /// </summary>
        public double[][] Geometry25832 { get; set; }

        public Dictionary<string, JsonElement> Attributes { get; set; }

        public AnalysisFeatureDto() { }

        public AnalysisFeatureDto(AnalysisFeature analysisFeature)
        {
            if (analysisFeature.Geometry is not LineString line)
                throw new ArgumentException("Expected LineString geometry");

            // Write only the 25832 geometry; omit the legacy 3857 coordinates
            Geometry25832 = line.Coordinates
                .Select(c => new[] { c.X, c.Y })
                .ToArray();

            Attributes = new Dictionary<string, JsonElement>();
            foreach (var field in analysisFeature.Fields)
            {
                var value = analysisFeature[field];
                if (IsDefault(value)) continue;
                Attributes[field] = JsonSerializer.SerializeToElement(value);
            }
        }

        public AnalysisFeature ToAnalysisFeature(JsonSerializerOptions? options = null)
        {
            // Prefer Geometry25832 (authoritative); fall back to legacy Coordinates for old files
            var coords = Geometry25832 ?? Coordinates
                ?? throw new InvalidOperationException("No geometry found in DTO");

            var geometry = new LineString(
                coords.Select(c => new Coordinate(c[0], c[1])).ToArray());

            var typedAttributes = DictionaryObjectConverter.ConvertAttributesToTyped(
                Attributes, typeof(AnalysisFeature), options);
            return new AnalysisFeature(geometry, typedAttributes);
        }

        private static bool IsDefault(object? value) => value switch
        {
            null => true,
            string s => s.Length == 0,
            int i => i == 0,
            double d => d == 0.0,
            bool b => !b,
            _ => false,
        };
    }
}
