using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    // DTO for Polyline
    public class PolylineDto
    {
        public List<Point2d> Vertices { get; set; } = new();
        public bool Closed { get; set; }
    }

    // Converter for Polyline <-> PolylineDto
    public class PolylineJsonConverter : JsonConverter<Polyline?>
    {
        public override Polyline? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dto = JsonSerializer.Deserialize<PolylineDto>(ref reader, options);
            if (dto == null) return null;
            var pline = new Polyline();
            for (int i = 0; i < dto.Vertices.Count; i++)
                pline.AddVertexAt(i, dto.Vertices[i], 0, 0, 0);
            pline.Closed = dto.Closed;
            return pline;
        }
        public override void Write(Utf8JsonWriter writer, Polyline? value, JsonSerializerOptions options)
        {
            if (value == null) { writer.WriteNullValue(); return; }
            var dto = new PolylineDto
            {
                Closed = value.Closed
            };
            for (int i = 0; i < value.NumberOfVertices; i++)
                dto.Vertices.Add(value.GetPoint2dAt(i));
            JsonSerializer.Serialize(writer, dto, options);
        }
    }

    // DTO for Extents2d
    public class Extents2dDto
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    // Converter for Extents2d <-> Extents2dDto
    public class Extents2dJsonConverter : JsonConverter<Extents2d>
    {
        public override Extents2d Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dto = JsonSerializer.Deserialize<Extents2dDto>(ref reader, options);
            if (dto == null) throw new JsonException();
            return new Extents2d(dto.MinX, dto.MinY, dto.MaxX, dto.MaxY);
        }
        public override void Write(Utf8JsonWriter writer, Extents2d value, JsonSerializerOptions options)
        {
            var dto = new Extents2dDto
            {
                MinX = value.MinPoint.X,
                MinY = value.MinPoint.Y,
                MaxX = value.MaxPoint.X,
                MaxY = value.MaxPoint.Y
            };
            JsonSerializer.Serialize(writer, dto, options);
        }
    }
}
