using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DimensioneringV2.Serialization
{
    class DimJsonConverter : JsonConverter<Dim>
    {
        public override Dim Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<Dim>(ref reader, options)!;
        }

        public override void Write(Utf8JsonWriter writer, Dim value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}