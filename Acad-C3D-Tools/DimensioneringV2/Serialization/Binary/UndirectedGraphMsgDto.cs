using DimensioneringV2.GraphFeatures;

using MessagePack;

using QuikGraph;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal partial class UndirectedGraphMsgDto
{
    [Key(0)] internal List<NodeJunctionMsgDto> Vertices { get; set; } = new();
    [Key(1)] internal List<EdgePipeSegmentMsgDto> Edges { get; set; } = new();

    internal static UndirectedGraphMsgDto FromDomain(
        UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
    {
        var dto = new UndirectedGraphMsgDto();

        // Build vertex list and index lookup
        var vertexList = graph.Vertices.ToList();
        var indexLookup = new Dictionary<NodeJunction, int>(vertexList.Count);
        for (int i = 0; i < vertexList.Count; i++)
        {
            indexLookup[vertexList[i]] = i;
            dto.Vertices.Add(NodeJunctionMsgDto.FromDomain(vertexList[i]));
        }

        // Build edges using indices
        foreach (var edge in graph.Edges)
        {
            dto.Edges.Add(new EdgePipeSegmentMsgDto
            {
                SourceIndex = indexLookup[edge.Source],
                TargetIndex = indexLookup[edge.Target],
                PipeSegment = AnalysisFeatureMsgDto.FromDomain(edge.PipeSegment),
                Level = edge.Level,
            });
        }

        return dto;
    }

    internal UndirectedGraph<NodeJunction, EdgePipeSegment> ToDomain()
    {
        // Reconstruct vertices
        var nodes = Vertices.Select(v => v.ToDomain()).ToList();

        // Build graph
        var graph = new UndirectedGraph<NodeJunction, EdgePipeSegment>();
        graph.AddVertexRange(nodes);

        foreach (var edgeDto in Edges)
        {
            var source = nodes[edgeDto.SourceIndex];
            var target = nodes[edgeDto.TargetIndex];
            var pipeSegment = edgeDto.PipeSegment.ToDomain();

            var edge = new EdgePipeSegment(source, target, pipeSegment)
            {
                Level = edgeDto.Level,
            };
            graph.AddEdge(edge);
        }

        return graph;
    }
}
