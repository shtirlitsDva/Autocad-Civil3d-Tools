using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal abstract class PolylineExportSegment
    {
        public int Index { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }

    internal class LineSegment : PolylineExportSegment
    {
        
    }

    internal class ArcSegment : PolylineExportSegment
    {        
        public double Radius { get; set; }
        public double CX { get; set; }
        public double CY { get; set; }
    }

    internal class PolylineSegmentConverter : JsonConverter<PolylineExportSegment>
    {
        public override PolylineExportSegment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotImplementedException("Deserialization not required.");

        public override void Write(Utf8JsonWriter writer, PolylineExportSegment value, JsonSerializerOptions options)
        {
            var type = value.GetType();
            JsonSerializer.Serialize(writer, value, type, options);
        }
    }
}
