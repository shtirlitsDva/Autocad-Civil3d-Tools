using DimensioneringV2.GraphFeatures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DimensioneringV2.Serialization
{
    internal class AnalysisFeatureJsonConverter : JsonConverter<AnalysisFeature>
    {
        public override AnalysisFeature? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Deserialize into the DTO
            var dto = JsonSerializer.Deserialize<AnalysisFeatureDto>(ref reader, options);
            if (dto == null)
                throw new JsonException("Failed to deserialize AnalysisFeatureDto.");

            // Rebuild the AnalysisFeature from the DTO
            var analysisFeature = dto.ToAnalysisFeature();
            return analysisFeature;
        }

        public override void Write(Utf8JsonWriter writer, AnalysisFeature value, JsonSerializerOptions options)
        {
            // Create DTO from the AnalysisFeature
            var dto = new AnalysisFeatureDto(value);

            // Serialize the DTO
            JsonSerializer.Serialize(writer, dto, options);
        }
    }
}