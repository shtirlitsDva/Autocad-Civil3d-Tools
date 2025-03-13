using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class UndirectedGraphJsonConverter : JsonConverter<UndirectedGraph<NodeJunction, EdgePipeSegment>>
    {
        public override UndirectedGraph<NodeJunction, EdgePipeSegment> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dto = JsonSerializer.Deserialize<UndirectedGraphDto>(ref reader, options);
            var graph = new UndirectedGraph<NodeJunction, EdgePipeSegment>();
            graph.AddVertexRange(dto.Vertices);
            foreach (var edge in dto.Edges)
            {
                graph.AddEdge(edge);
            }
            return graph;
        }

        public override void Write(Utf8JsonWriter writer, UndirectedGraph<NodeJunction, EdgePipeSegment> value, JsonSerializerOptions options)
        {
            var dto = new UndirectedGraphDto(value);
            JsonSerializer.Serialize(writer, dto, options);
        }
    }
}