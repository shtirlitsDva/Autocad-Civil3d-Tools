using System.Windows;

namespace IntersectUtilities.MPE.MapConnections;

internal sealed record GraphNode(string Name, bool IsNa, bool HasView, Point Center);

/// <summary>
/// A connection as drawn in the graph. <see cref="TreeParent"/> is the end that sits above the other in the
/// spanning tree (null for a cross edge); the arrow still points From → To, which may be upwards.
/// </summary>
internal sealed record GraphEdge(LpConnection Connection, string? TreeParent)
{
    public bool IsTreeEdge => TreeParent is not null;
}

/// <summary>
/// Top-down tree layout for the "Graf" view, in layout units (≈ pixels at zoom 1). Each connected network is
/// hung from its best-connected LP and spread breadth-first, so the tree stays shallow and compact; children
/// are ordered by the station they meet the parent at. The network is not a strict tree (a branch can touch
/// two mains), so the edges left out of the spanning tree are drawn across it, dashed.
/// </summary>
internal sealed class MapConnectionsGraphLayout
{
    public const double NodeWidth = 70.0;
    public const double NodeHeight = 26.0;
    private const double SlotWidth = 190.0;
    private const double LayerGap = 120.0;

    private MapConnectionsGraphLayout(IReadOnlyDictionary<string, GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
    }

    public IReadOnlyDictionary<string, GraphNode> Nodes { get; }
    public IReadOnlyList<GraphEdge> Edges { get; }

    public static MapConnectionsGraphLayout Build(LpNetworkSnapshot snapshot)
    {
        HashSet<string> naNames = snapshot.Connections
            .Where(c => c.Kind == LpConnectionKind.Na).Select(c => c.To).ToHashSet();
        List<string> names = snapshot.Lines.Keys.Concat(naNames).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();

        Dictionary<string, List<LpConnection>> incident = names.ToDictionary(n => n, _ => new List<LpConnection>());
        foreach (LpConnection c in snapshot.Connections)
        {
            incident[c.From].Add(c);
            incident[c.To].Add(c);
        }

        Dictionary<string, List<string>> children = names.ToDictionary(n => n, _ => new List<string>());
        Dictionary<LpConnection, string> treeParentOf = new();
        HashSet<string> visited = new();
        List<string> roots = [];

        // Best-connected LP first, so every network is hung from its hub.
        foreach (string root in names.OrderByDescending(n => incident[n].Count).ThenBy(n => n, StringComparer.Ordinal))
        {
            if (!visited.Add(root))
            {
                continue;
            }

            roots.Add(root);
            Queue<string> queue = new();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                string node = queue.Dequeue();
                foreach (LpConnection edge in incident[node].OrderBy(c => LpConnectionResolver.StationOn(c, node)))
                {
                    string other = edge.From == node ? edge.To : edge.From;
                    if (visited.Add(other))
                    {
                        children[node].Add(other);
                        treeParentOf[edge] = node;
                        queue.Enqueue(other);
                    }
                }
            }
        }

        // Tidy placement: each node gets as many slots as its subtree has leaves and is centred over them.
        Dictionary<string, double> widths = new();
        double Width(string node) =>
            widths.TryGetValue(node, out double w) ? w : widths[node] = Math.Max(1.0, children[node].Sum(Width));

        Dictionary<string, GraphNode> nodes = new();
        void Place(string node, double left, int depth)
        {
            bool isNa = naNames.Contains(node) && !snapshot.Lines.ContainsKey(node);
            bool hasView = snapshot.Lines.TryGetValue(node, out LpLine? line) && line.View is not null;
            nodes[node] = new GraphNode(node, isNa, hasView, new Point((left + Width(node) / 2.0) * SlotWidth, depth * LayerGap));
            double cursor = left;
            foreach (string child in children[node])
            {
                Place(child, cursor, depth + 1);
                cursor += Width(child);
            }
        }

        // Biggest networks first, lone LPs trailing on the right.
        double offset = 0.0;
        foreach (string root in roots.OrderByDescending(Width).ThenBy(r => r, StringComparer.Ordinal))
        {
            Place(root, offset, 0);
            offset += Width(root);
        }

        List<GraphEdge> edges = snapshot.Connections
            .Select(c => new GraphEdge(c, treeParentOf.TryGetValue(c, out string? parent) ? parent : null))
            .ToList();
        return new MapConnectionsGraphLayout(nodes, edges);
    }
}
