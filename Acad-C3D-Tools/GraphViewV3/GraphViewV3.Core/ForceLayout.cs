namespace GraphViewV3.Core;

/// <summary>
/// Fruchterman–Reingold force-directed layout, run per connected component so that
/// disconnected partial networks and single just-placed entities FLOAT independently.
/// Each component is laid out around its own origin, then packed into a loose grid of
/// cells sized to the largest component, so clusters never overlap.
/// Deterministic: initial positions come from a seeded hash of the node handle, so a
/// rebuild of an unchanged drawing yields a stable picture (no random jitter on refresh).
/// </summary>
public sealed class ForceLayout
{
    public int Iterations { get; init; } = 120;
    public double Area { get; init; } = 1_000_000.0; // k = sqrt(area / n)
    public double ComponentPadding { get; init; } = 120.0;

    public void Apply(NetworkGraph graph)
    {
        if (graph.Nodes.Count == 0) return;

        var laidOut = new List<(IReadOnlyList<GraphNode> Nodes, double W, double H)>();
        foreach (var comp in graph.Components)
        {
            LayoutComponent(comp, graph);
            var (w, h) = Normalize(comp);
            laidOut.Add((comp, w, h));
        }

        // Pack components left-to-right, wrapping rows — each floats in its own cell.
        double cellMax = 0;
        foreach (var c in laidOut) cellMax = Math.Max(cellMax, Math.Max(c.W, c.H));
        double cell = cellMax + ComponentPadding;
        int cols = (int)Math.Ceiling(Math.Sqrt(laidOut.Count));
        if (cols < 1) cols = 1;

        for (int i = 0; i < laidOut.Count; i++)
        {
            int row = i / cols, col = i % cols;
            double ox = col * cell, oy = row * cell;
            foreach (var n in laidOut[i].Nodes) { n.X += ox; n.Y += oy; }
        }
    }

    private void LayoutComponent(IReadOnlyList<GraphNode> nodes, NetworkGraph graph)
    {
        int n = nodes.Count;
        if (n == 1) { nodes[0].X = 0; nodes[0].Y = 0; return; }

        double k = Math.Sqrt(Area / n);
        var index = new Dictionary<GraphNode, int>(n);
        for (int i = 0; i < n; i++)
        {
            index[nodes[i]] = i;
            // Deterministic seed circle from the handle hash.
            uint h = Fnv(nodes[i].Handle);
            double ang = (h % 3600) / 3600.0 * 2 * Math.PI;
            double rad = k * (0.5 + (h >> 16) % 1000 / 1000.0);
            nodes[i].X = Math.Cos(ang) * rad;
            nodes[i].Y = Math.Sin(ang) * rad;
        }

        var edges = new List<(int, int)>();
        foreach (var e in graph.Edges)
            if (index.TryGetValue(e.A, out int ia) && index.TryGetValue(e.B, out int ib))
                edges.Add((ia, ib));

        var dispX = new double[n];
        var dispY = new double[n];
        double t = k * 2; // initial "temperature"
        double cool = t / (Iterations + 1);

        for (int it = 0; it < Iterations; it++)
        {
            Array.Clear(dispX); Array.Clear(dispY);

            // Repulsion (O(n^2); n per component is small for live FJV networks).
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double dx = nodes[i].X - nodes[j].X;
                    double dy = nodes[i].Y - nodes[j].Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy) + 1e-6;
                    double rep = k * k / dist;
                    double ux = dx / dist, uy = dy / dist;
                    dispX[i] += ux * rep; dispY[i] += uy * rep;
                    dispX[j] -= ux * rep; dispY[j] -= uy * rep;
                }

            // Attraction along edges.
            foreach (var (a, b) in edges)
            {
                double dx = nodes[a].X - nodes[b].X;
                double dy = nodes[a].Y - nodes[b].Y;
                double dist = Math.Sqrt(dx * dx + dy * dy) + 1e-6;
                double att = dist * dist / k;
                double ux = dx / dist, uy = dy / dist;
                dispX[a] -= ux * att; dispY[a] -= uy * att;
                dispX[b] += ux * att; dispY[b] += uy * att;
            }

            for (int i = 0; i < n; i++)
            {
                double d = Math.Sqrt(dispX[i] * dispX[i] + dispY[i] * dispY[i]) + 1e-6;
                double lim = Math.Min(d, t);
                nodes[i].X += dispX[i] / d * lim;
                nodes[i].Y += dispY[i] / d * lim;
            }
            t -= cool;
        }
    }

    private static (double W, double H) Normalize(IReadOnlyList<GraphNode> nodes)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var node in nodes)
        {
            minX = Math.Min(minX, node.X); minY = Math.Min(minY, node.Y);
            maxX = Math.Max(maxX, node.X); maxY = Math.Max(maxY, node.Y);
        }
        foreach (var node in nodes) { node.X -= minX; node.Y -= minY; }
        return (maxX - minX, maxY - minY);
    }

    private static uint Fnv(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }
    }
}
