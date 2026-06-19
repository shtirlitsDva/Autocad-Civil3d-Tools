using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// Turns a <see cref="NSAlignmentCrawlSnapshot"/> into a weighted crawl graph.
///
/// Connectivity is purely geometric: any two connection points within
/// <see cref="NSAlignmentCrawlConstants.Tolerance"/> become the SAME node. A pipe endpoint
/// landing on a block port therefore unifies into one node — that coincidence IS the connection.
///
/// Each pipe becomes one edge carrying its real geometry. Each component becomes a star:
/// a centre node with one straight spoke to every port, so a crawl naturally routes
/// port → centre → port (a corner at a bend, a junction at a tee).
/// </summary>
internal static class NSAlignmentCrawlGraphBuilder
{
    public static CrawlNetwork Build(NSAlignmentCrawlSnapshot snapshot)
    {
        CrawlNetwork net = new();
        NodeClusterer clusterer = new(net);

        foreach (CrawlPipe pipe in snapshot.Pipes)
        {
            if (pipe.Vertices.Count < 2)
            {
                continue;
            }

            Polyline pl = new();
            for (int i = 0; i < pipe.Vertices.Count; i++)
            {
                pl.AddVertexAt(i, pipe.Vertices[i].Pt, pipe.Vertices[i].Bulge, 0.0, 0.0);
            }

            pl.Closed = false;

            int from = clusterer.GetOrAdd(pipe.Vertices[0].Pt);
            int to = clusterer.GetOrAdd(pipe.Vertices[^1].Pt);
            if (from == to)
            {
                // Degenerate (closed loop or zero-length) pipe — not crawlable in the prototype.
                pl.Dispose();
                continue;
            }

            net.AddPipeEdge(from, to, pl);
        }

        // Weld-on studs attach to pipes mid-span / across a gap, so their port nodes get a dedicated
        // wiring pass after all pipes and components exist.
        List<int> weldEndpoints = [];

        foreach (CrawlComponent component in snapshot.Components)
        {
            // A 2-port component carries a simple centreline chain between its two ports — use the real
            // geometry so curved fittings (BUERØR, radiused bends) crawl along their true arc. Branched
            // components (3-port tees/Y) would need mid-segment splitting and are never curved here, so
            // they keep the straight-star model below, which is faithful for straight junctions.
            if (component.Ports.Count == 2 && component.Centerlines.Count > 0)
            {
                foreach (IReadOnlyList<(Point2d Pt, double Bulge)> centerline in component.Centerlines)
                {
                    if (centerline.Count < 2)
                    {
                        continue;
                    }

                    Polyline pl = new();
                    for (int i = 0; i < centerline.Count; i++)
                    {
                        pl.AddVertexAt(i, centerline[i].Pt, centerline[i].Bulge, 0.0, 0.0);
                    }

                    pl.Closed = false;
                    int a = clusterer.GetOrAdd(centerline[0].Pt);
                    int b = clusterer.GetOrAdd(centerline[^1].Pt);
                    if (a == b)
                    {
                        pl.Dispose();
                        continue;
                    }

                    net.AddPipeEdge(a, b, pl);
                    if (component.IsWeldStud)
                    {
                        weldEndpoints.Add(a);
                        weldEndpoints.Add(b);
                    }
                }

                continue;
            }

            if (component.Ports.Count < 2)
            {
                // A 1-port stub (end cap / stik) cannot be passed through.
                continue;
            }

            int centerNode = clusterer.GetOrAdd(component.Center);
            foreach (Point2d port in component.Ports)
            {
                int portNode = clusterer.GetOrAdd(port);
                if (component.IsWeldStud)
                {
                    weldEndpoints.Add(portNode);
                }

                if (portNode == centerNode)
                {
                    continue;
                }

                double weight = component.Center.GetDistanceTo(port);
                net.AddStraightEdge(portNode, centerNode, weight);
            }
        }

        foreach (int node in weldEndpoints.Distinct())
        {
            ConnectWeldEndpoint(net, clusterer, node);
        }

        net.RebuildAdjacency();
        return net;
    }

    /// <summary>
    /// Wires a weld-on stud's port node onto the network: finds the nearest pipe, and either splits
    /// it (mid-span weld — so the crawl can continue along the carrier past the weld) or bridges to
    /// its endpoint (branch port sitting a little short of its pipe). No-op if the closest point is
    /// already the stud node (the weld sits exactly on the pipe).
    /// </summary>
    private static void ConnectWeldEndpoint(CrawlNetwork net, NodeClusterer clusterer, int studNode)
    {
        Point2d sp = net.Nodes[studNode].Position;
        Point3d p = new(sp.X, sp.Y, 0.0);

        NetworkEdge? best = null;
        Point3d bestCp = Point3d.Origin;
        double bestDist = double.MaxValue;
        foreach (NetworkEdge e in net.Edges)
        {
            if (e.Removed || e.Kind != EdgeKind.Pipe || e.Curve is null)
            {
                continue;
            }

            // Skip edges already incident to this node — otherwise the stud's own centreline edge
            // (whose endpoint IS the stud node, distance 0) always wins and the carrier/branch pipe
            // is never found.
            if (e.FromNode == studNode || e.ToNode == studNode)
            {
                continue;
            }

            Point3d cp;
            try
            {
                cp = e.Curve.GetClosestPointTo(p, false);
            }
            catch
            {
                continue;
            }

            double d = cp.DistanceTo(p);
            if (d < bestDist)
            {
                bestDist = d;
                best = e;
                bestCp = cp;
            }
        }

        if (best is null || bestDist > NSAlignmentCrawlConstants.StudConnectTolerance)
        {
            return; // no carrier/branch pipe within reach
        }

        double length = best.Curve!.Length;
        double distFromStart;
        try
        {
            distFromStart = best.Curve.GetDistAtPoint(bestCp);
        }
        catch
        {
            return;
        }

        int targetNode;
        if (distFromStart <= NSAlignmentCrawlConstants.Tolerance)
        {
            targetNode = best.FromNode;
        }
        else if (length - distFromStart <= NSAlignmentCrawlConstants.Tolerance)
        {
            targetNode = best.ToNode;
        }
        else
        {
            // Mid-span weld: split the carrier so the crawl can continue along it past the weld.
            if (!TrySplit(best.Curve, bestCp, out Polyline seg1, out Polyline seg2))
            {
                return;
            }

            targetNode = clusterer.GetOrAdd(new Point2d(bestCp.X, bestCp.Y));
            best.Removed = true;
            best.Curve.Dispose();
            best.Curve = null;
            net.AddPipeEdge(best.FromNode, targetNode, seg1);
            net.AddPipeEdge(targetNode, best.ToNode, seg2);
        }

        if (targetNode != studNode)
        {
            // Short straight connector across the small gap between the stud port and its pipe.
            double weight = net.Nodes[studNode].Position.GetDistanceTo(net.Nodes[targetNode].Position);
            net.AddStraightEdge(studNode, targetNode, weight);
        }
    }

    private static bool TrySplit(Polyline curve, Point3d at, out Polyline seg1, out Polyline seg2)
    {
        seg1 = null!;
        seg2 = null!;
        try
        {
            double param = curve.GetParameterAtPoint(at);
            DBObjectCollection objs = curve.GetSplitCurves(new DoubleCollection([param]));
            if (objs.Count != 2 || objs[0] is not Polyline s1 || objs[1] is not Polyline s2)
            {
                foreach (DBObject o in objs)
                {
                    o.Dispose();
                }

                return false;
            }

            seg1 = s1;
            seg2 = s2;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cheap 1 mm point clustering via a spatial hash. Points are bucketed on a tolerance-sized
    /// grid; a lookup probes the 3×3 neighbourhood so matches that straddle a cell boundary are
    /// still found.
    /// </summary>
    private sealed class NodeClusterer(CrawlNetwork net)
    {
        private const double Tol = NSAlignmentCrawlConstants.Tolerance;
        private readonly Dictionary<(long, long), List<int>> _cells = [];

        public int GetOrAdd(Point2d p)
        {
            long kx = Key(p.X);
            long ky = Key(p.Y);

            for (long dx = -1; dx <= 1; dx++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    if (!_cells.TryGetValue((kx + dx, ky + dy), out List<int>? bucket))
                    {
                        continue;
                    }

                    foreach (int idx in bucket)
                    {
                        if (net.Nodes[idx].Position.GetDistanceTo(p) <= Tol)
                        {
                            return idx;
                        }
                    }
                }
            }

            int newIndex = net.AddNode(p);
            if (!_cells.TryGetValue((kx, ky), out List<int>? home))
            {
                home = [];
                _cells[(kx, ky)] = home;
            }

            home.Add(newIndex);
            return newIndex;
        }

        private static long Key(double v) => (long)Math.Round(v / Tol);
    }
}
