namespace GraphViewV3.Core;

/// <summary>
/// Builds an in-memory network graph from a snapshot using pure geometry:
/// every entity contributes "ports" (pipe endpoints, component MuffeIntern points);
/// ports that coincide within <see cref="Tolerance"/> connect their owning entities.
/// A spatial hash grid keeps this near-O(n); union-find groups the floating components.
///
/// Two domain rules from the FJV ruleset are encoded here:
///  • A direct pipe-to-pipe connection is illegal — kept as an edge but flagged IsError.
///  • Welded studs (afgrstuds / sh-lige / sh-vinkel) connect via their main WeldPort landing
///    anywhere ALONG a pipe (point-on-segment), not only at a vertex.
///
/// No PropertySets, no AutoCAD — fully testable.
/// </summary>
public sealed class ConnectivityBuilder
{
    /// <summary>Max distance (drawing units, ~metres) between two ports for them to count
    /// as the same connection. Determined empirically; couplings are snapped so this is small.</summary>
    public double Tolerance { get; init; } = 0.5;

    private sealed record Port(int NodeIndex, Pt P);

    public NetworkGraph Build(NetworkSnapshot snapshot)
    {
        var nodes = new List<GraphNode>(snapshot.Pipes.Count + snapshot.Components.Count);
        var ports = new List<Port>();

        // Pipes occupy node indices [0 .. Pipes.Count); component j -> Pipes.Count + j.
        foreach (var p in snapshot.Pipes)
        {
            int idx = nodes.Count;
            nodes.Add(new GraphNode
            {
                Handle = p.Handle,
                Kind = NodeKind.Pipe,
                Label = string.IsNullOrEmpty(p.Size) ? "Rør" : p.Size,
                System = p.System,
                Size = p.Size,
                Length = p.Length,
            });
            ports.Add(new Port(idx, p.P0));
            ports.Add(new Port(idx, p.P1));
        }

        int firstComponentIndex = nodes.Count;
        foreach (var c in snapshot.Components)
        {
            int idx = nodes.Count;
            nodes.Add(new GraphNode
            {
                Handle = c.Handle,
                Kind = NodeKind.Component,
                Label = c.Name,
            });
            if (c.Ports.Count == 0)
                ports.Add(new Port(idx, c.Position));
            else
                foreach (var port in c.Ports) ports.Add(new Port(idx, port));
        }

        if (nodes.Count == 0) return NetworkGraph.Empty;

        var uf = new UnionFind(nodes.Count);
        var edgePairs = new HashSet<(int, int)>();

        void Connect(int a, int b)
        {
            if (a == b) return;
            uf.Union(a, b);
            edgePairs.Add((Math.Min(a, b), Math.Max(a, b)));
        }

        // --- Coincident-port matching via a spatial hash (cell == tolerance, 3x3 probe). ---
        double cell = Tolerance <= 0 ? 1e-6 : Tolerance;
        var grid = new Dictionary<(long, long), List<Port>>();
        (long, long) Key(Pt p) => ((long)Math.Floor(p.X / cell), (long)Math.Floor(p.Y / cell));

        foreach (var port in ports)
        {
            var k = Key(port.P);
            for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                    if (grid.TryGetValue((k.Item1 + dx, k.Item2 + dy), out var bucket))
                        foreach (var other in bucket)
                        {
                            if (other.NodeIndex == port.NodeIndex) continue;
                            if (port.P.DistanceTo(other.P) > Tolerance) continue;
                            Connect(port.NodeIndex, other.NodeIndex);
                        }
            if (!grid.TryGetValue(k, out var self)) grid[k] = self = new List<Port>();
            self.Add(port);
        }

        // --- Mid-span welded studs: weld port landing along a pipe segment interior. ---
        for (int j = 0; j < snapshot.Components.Count; j++)
        {
            var c = snapshot.Components[j];
            if (!c.Weldable || c.WeldPort is not { } wp) continue;
            int compIdx = firstComponentIndex + j;
            for (int i = 0; i < snapshot.Pipes.Count; i++)
            {
                if (PointOnPolyline(wp, snapshot.Pipes[i].Vertices, Tolerance))
                    Connect(compIdx, i);
            }
        }

        // Adjacency, for detecting pipe junctions that are actually mediated by a component.
        var adj = new Dictionary<int, HashSet<int>>();
        void Link(int a, int b)
        {
            if (!adj.TryGetValue(a, out var s)) adj[a] = s = new HashSet<int>();
            s.Add(b);
        }
        foreach (var (a, b) in edgePairs) { Link(a, b); Link(b, a); }

        // A pipe-pipe junction is legal when a component sits at it (both pipes share a
        // component neighbour) — that direct edge is then a tolerance artifact, so drop it.
        // A pipe-pipe junction with NO mediating component is the illegal case: flag it.
        bool Mediated(int a, int b)
        {
            if (!adj.TryGetValue(a, out var na) || !adj.TryGetValue(b, out var nb)) return false;
            foreach (var x in na)
                if (x != b && nodes[x].Kind == NodeKind.Component && nb.Contains(x)) return true;
            return false;
        }

        // --- Materialise edges; flag genuine direct pipe-to-pipe as illegal. ---
        var edges = new List<GraphEdge>(edgePairs.Count);
        foreach (var (a, b) in edgePairs)
        {
            bool pipePipe = nodes[a].Kind == NodeKind.Pipe && nodes[b].Kind == NodeKind.Pipe;
            if (pipePipe && Mediated(a, b)) continue; // real path is pipe-component-pipe
            edges.Add(new GraphEdge
            {
                A = nodes[a],
                B = nodes[b],
                IsError = pipePipe,
                ErrorReason = pipePipe ? "Direct pipe-to-pipe connection (must join through a component)" : null,
            });
        }

        // --- Group nodes into connected components. ---
        var byRoot = new Dictionary<int, List<GraphNode>>();
        for (int i = 0; i < nodes.Count; i++)
        {
            int root = uf.Find(i);
            if (!byRoot.TryGetValue(root, out var list)) byRoot[root] = list = new List<GraphNode>();
            list.Add(nodes[i]);
        }

        var components = new List<IReadOnlyList<GraphNode>>(byRoot.Count);
        int compId = 0;
        foreach (var list in byRoot.Values)
        {
            foreach (var n in list) n.ComponentId = compId;
            components.Add(list);
            compId++;
        }

        return new NetworkGraph(nodes, edges, components);
    }

    /// <summary>True if <paramref name="p"/> lies within tol of any segment of the polyline.</summary>
    private static bool PointOnPolyline(Pt p, IReadOnlyList<Pt> verts, double tol)
    {
        if (verts.Count < 2) return false;
        for (int i = 0; i < verts.Count - 1; i++)
            if (DistanceToSegment(p, verts[i], verts[i + 1]) <= tol)
                return true;
        return false;
    }

    private static double DistanceToSegment(Pt p, Pt a, Pt b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-12) return p.DistanceTo(a);
        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
        t = Math.Max(0, Math.Min(1, t));
        var proj = new Pt(a.X + t * dx, a.Y + t * dy);
        return p.DistanceTo(proj);
    }

    private sealed class UnionFind
    {
        private readonly int[] _parent;
        private readonly int[] _rank;
        public UnionFind(int n)
        {
            _parent = new int[n];
            _rank = new int[n];
            for (int i = 0; i < n; i++) _parent[i] = i;
        }
        public int Find(int x)
        {
            while (_parent[x] != x) { _parent[x] = _parent[_parent[x]]; x = _parent[x]; }
            return x;
        }
        public void Union(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra == rb) return;
            if (_rank[ra] < _rank[rb]) (ra, rb) = (rb, ra);
            _parent[rb] = ra;
            if (_rank[ra] == _rank[rb]) _rank[ra]++;
        }
    }
}
