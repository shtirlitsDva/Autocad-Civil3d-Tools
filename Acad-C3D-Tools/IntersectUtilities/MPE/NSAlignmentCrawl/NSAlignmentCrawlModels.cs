using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// Tunables for the crawl prototype. The connection/cluster tolerance is the one physical
/// assumption: pipe endpoints and block ports that lie within this distance are treated as the
/// same connection node. Measured on real FV_Fremtid drawings, pipe ends sit up to ~10 mm from
/// their block port (not the 1 mm originally assumed), while two ports of the same component are
/// never closer than ~100 mm — so 25 mm connects reliably without ever merging distinct ports.
/// </summary>
internal static class NSAlignmentCrawlConstants
{
    public const double Tolerance = 0.025; // metres (25 mm)

    // Two straight segments meeting at less than this angle are treated as one line: the shared
    // vertex is a redundant node on a straight run and gets weeded out of the baked polyline. Kept
    // deliberately tight (~0.057°) — real direction changes at fittings are whole degrees, and
    // elastic bends are arcs (carried as bulges, never as chords), so this only removes drafting/
    // split noise and never collapses an intended bend.
    public const double CollinearAngleTolerance = 0.001; // radians

    // Weld-on studs (AFGRSTUDS / SH LIGE) attach loosely: the branch port can sit ~100 mm from its
    // pipe. This larger tolerance is used only when wiring a stud's ports onto the network (split or
    // bridge), never for general clustering, so distinct ports are never merged.
    public const double StudConnectTolerance = 0.3; // metres (300 mm)

    // Substring, matched case-insensitively (see IsTargetXref), so this catches any xref whose
    // name contains "Fremtid" regardless of project prefix/suffix or casing.
    public const string XrefName = "Fremtid";
    public const string OutputLayer = "0";
}

/// <summary>A pipe read from the xref, in host WCS, as ordered vertices + bulges (2D).</summary>
internal sealed record CrawlPipe(IReadOnlyList<(Point2d Pt, double Bulge)> Vertices);

/// <summary>
/// A component block read from the xref: its centre, MuffeIntern port positions, and (when present)
/// the real centreline curves drawn on a "*komponent*" layer inside the block — each a vertex+bulge
/// chain in host WCS. Centrelines let curved fittings (e.g. BUERØR) crawl along their true arc instead
/// of a straight chord. All in 2D host WCS.
/// </summary>
internal sealed record CrawlComponent(
    Point2d Center,
    IReadOnlyList<Point2d> Ports,
    IReadOnlyList<IReadOnlyList<(Point2d Pt, double Bulge)>> Centerlines,
    bool IsWeldStud);

/// <summary>The raw, transaction-free snapshot of the FV_Fremtid network.</summary>
internal sealed class NSAlignmentCrawlSnapshot
{
    public List<CrawlPipe> Pipes { get; } = [];
    public List<CrawlComponent> Components { get; } = [];
}

internal enum EdgeKind
{
    /// <summary>Carries real pipe geometry (lines + arcs) in <see cref="NetworkEdge.Curve"/>.</summary>
    Pipe,

    /// <summary>A straight spoke between a block port and the block centre; geometry is the two node positions.</summary>
    Straight,
}

internal sealed class NetworkNode
{
    public int Index { get; init; }
    public Point2d Position { get; init; }
}

/// <summary>
/// A graph edge. For <see cref="EdgeKind.Pipe"/> edges <see cref="Curve"/> holds an in-memory
/// (non-DB) Polyline oriented <see cref="FromNode"/> → <see cref="ToNode"/> so arcs reproduce on
/// output; for <see cref="EdgeKind.Straight"/> edges <see cref="Curve"/> is null and the geometry
/// is just the two endpoint node positions.
/// </summary>
internal sealed class NetworkEdge
{
    public int Index { get; init; }
    public int FromNode { get; init; }
    public int ToNode { get; init; }
    public double Weight { get; set; }
    public EdgeKind Kind { get; init; }
    public Polyline? Curve { get; set; }

    /// <summary>Spliced-out edges are kept for stable indexing but excluded from adjacency/traversal.</summary>
    public bool Removed { get; set; }
}

/// <summary>
/// The in-memory crawl graph. Owns the pipe edges' in-memory Polylines and disposes them.
/// Pure data once built — holds no DB objects, so it survives across the interactive prompt loop.
/// </summary>
internal sealed class CrawlNetwork : IDisposable
{
    public List<NetworkNode> Nodes { get; } = [];
    public List<NetworkEdge> Edges { get; } = [];

    /// <summary>node index → indices of incident, non-removed edges. Rebuilt after splicing.</summary>
    public List<List<int>> Adjacency { get; } = [];

    public int AddNode(Point2d position)
    {
        int index = Nodes.Count;
        Nodes.Add(new NetworkNode { Index = index, Position = position });
        return index;
    }

    public NetworkEdge AddPipeEdge(int fromNode, int toNode, Polyline curve)
    {
        NetworkEdge edge = new()
        {
            Index = Edges.Count,
            FromNode = fromNode,
            ToNode = toNode,
            Kind = EdgeKind.Pipe,
            Curve = curve,
            Weight = curve.Length,
        };
        Edges.Add(edge);
        return edge;
    }

    public NetworkEdge AddStraightEdge(int fromNode, int toNode, double weight)
    {
        NetworkEdge edge = new()
        {
            Index = Edges.Count,
            FromNode = fromNode,
            ToNode = toNode,
            Kind = EdgeKind.Straight,
            Curve = null,
            Weight = weight,
        };
        Edges.Add(edge);
        return edge;
    }

    /// <summary>Rebuilds adjacency from the current (non-removed) edge set. Call after any splice.</summary>
    public void RebuildAdjacency()
    {
        Adjacency.Clear();
        for (int i = 0; i < Nodes.Count; i++)
        {
            Adjacency.Add([]);
        }

        foreach (NetworkEdge edge in Edges)
        {
            if (edge.Removed)
            {
                continue;
            }

            Adjacency[edge.FromNode].Add(edge.Index);
            Adjacency[edge.ToNode].Add(edge.Index);
        }
    }

    public void Dispose()
    {
        foreach (NetworkEdge edge in Edges)
        {
            edge.Curve?.Dispose();
            edge.Curve = null;
        }
    }
}
