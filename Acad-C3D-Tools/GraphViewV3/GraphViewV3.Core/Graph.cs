namespace GraphViewV3.Core;

public enum NodeKind { Pipe, Component }

/// <summary>A graph node = one drawing entity (pipe or component). X/Y are filled by
/// the layout pass; they are mutable so the force layout can iterate in place.</summary>
public sealed class GraphNode
{
    public required string Handle { get; init; }
    public required NodeKind Kind { get; init; }
    public required string Label { get; init; }
    public string System { get; init; } = "";
    public string Size { get; init; } = "";
    public double Length { get; init; }

    /// <summary>Index of the connected component this node belongs to (set by builder).</summary>
    public int ComponentId { get; set; } = -1;

    public double X;
    public double Y;
}

public sealed class GraphEdge
{
    public required GraphNode A { get; init; }
    public required GraphNode B { get; init; }

    /// <summary>True for connections that violate a rule (e.g. a direct pipe-to-pipe
    /// connection, which is illegal — pipes must join through a component). Rendered red.</summary>
    public bool IsError { get; init; }

    /// <summary>Optional human-readable reason for an error edge.</summary>
    public string? ErrorReason { get; init; }
}

/// <summary>The built network graph: nodes, edges, and the disconnected components
/// (each a free-floating cluster on the canvas).</summary>
public sealed class NetworkGraph
{
    public IReadOnlyList<GraphNode> Nodes { get; }
    public IReadOnlyList<GraphEdge> Edges { get; }
    public IReadOnlyList<IReadOnlyList<GraphNode>> Components { get; }

    public NetworkGraph(
        IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<GraphEdge> edges,
        IReadOnlyList<IReadOnlyList<GraphNode>> components)
    {
        Nodes = nodes;
        Edges = edges;
        Components = components;
    }

    public static readonly NetworkGraph Empty = new(
        Array.Empty<GraphNode>(),
        Array.Empty<GraphEdge>(),
        Array.Empty<IReadOnlyList<GraphNode>>());
}
