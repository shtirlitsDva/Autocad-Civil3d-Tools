using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// Holds the crawl state for one start point: it splices the start point into its pipe as a new
/// node, runs single-source Dijkstra ONCE from that node, then resolves the shortest path to any
/// (moving) end point cheaply via the cached dist[]/prev[] arrays — so the live preview never
/// re-searches the graph.
///
/// "Anywhere on a pipe": both the start and the end snap to the closest point on the nearest pipe,
/// and the pipe geometry is split there (GetSplitCurves) so arcs stay correct at the cut.
/// </summary>
internal sealed class CrawlSession
{
    private const double Tol = NSAlignmentCrawlConstants.Tolerance;

    private readonly CrawlNetwork _net;
    private readonly int _startNode;
    private double[] _dist = [];
    private int[] _prevEdge = [];
    private int[] _prevNode = [];

    private CrawlSession(CrawlNetwork net, int startNode)
    {
        _net = net;
        _startNode = startNode;
    }

    /// <summary>Snaps the start to the nearest pipe, splices a start node, and runs Dijkstra.</summary>
    public static CrawlSession? Create(CrawlNetwork net, Point3d startPick, out string error)
    {
        error = string.Empty;
        Point3d pick = Flatten(startPick);

        if (!TryResolveSnap(net, pick, out SnapTarget snap))
        {
            error = "Startpunktet er ikke i nærheden af nettet.";
            return null;
        }

        int startNode;
        if (snap.OnNode)
        {
            // Snap straight onto a connection node — a pipe junction or a terminal component port.
            startNode = snap.NodeIndex;
        }
        else
        {
            NetworkEdge edge = net.Edges[snap.EdgeIndex];
            double length = edge.Curve!.Length;
            if (snap.DistFromStart <= Tol)
            {
                startNode = edge.FromNode;
            }
            else if (length - snap.DistFromStart <= Tol)
            {
                startNode = edge.ToNode;
            }
            else
            {
                if (!TrySplit(edge.Curve!, snap.OnCurve, out Polyline seg1, out Polyline seg2))
                {
                    error = "Kunne ikke opdele røret ved startpunktet.";
                    return null;
                }

                int sNode = net.AddNode(new Point2d(snap.OnCurve.X, snap.OnCurve.Y));
                net.AddPipeEdge(edge.FromNode, sNode, seg1);
                net.AddPipeEdge(sNode, edge.ToNode, seg2);
                edge.Removed = true;
                edge.Curve!.Dispose();
                edge.Curve = null;
                startNode = sNode;
            }
        }

        net.RebuildAdjacency();
        CrawlSession session = new(net, startNode);
        session.RunDijkstra();
        return session;
    }

    /// <summary>
    /// Resolves the shortest crawl path from the fixed start to <paramref name="endPick"/> and
    /// returns it as (point, outgoing-bulge) vertices ready for the polyline builder.
    /// </summary>
    public bool TryBuildPath(Point3d endPick, out List<(Point2d Pt, double OutBulge)> vertices)
    {
        vertices = [];
        Point3d pick = Flatten(endPick);

        if (!TryResolveSnap(_net, pick, out SnapTarget snap))
        {
            return false;
        }

        // End snapped directly onto a connection node (e.g. a terminal angle component's free port):
        // the path simply ends there, with no mid-pipe remainder.
        if (snap.OnNode)
        {
            if (double.IsInfinity(_dist[snap.NodeIndex]))
            {
                return false;
            }

            if (!TryReconstructEdges(snap.NodeIndex, out List<int> nodePath))
            {
                return false;
            }

            List<(Point2d Pt, double OutBulge)> nodeResult = AssembleEdges(nodePath);
            if (nodeResult.Count < 2)
            {
                return false;
            }

            vertices = nodeResult;
            return true;
        }

        NetworkEdge edge = _net.Edges[snap.EdgeIndex];
        Point3d onCurve = snap.OnCurve;
        double distFromStart = snap.DistFromStart;
        double length = edge.Curve!.Length;
        int cNode = edge.FromNode;
        int dNode = edge.ToNode;

        int endAtNode = -1;
        if (distFromStart <= Tol)
        {
            endAtNode = cNode;
        }
        else if (length - distFromStart <= Tol)
        {
            endAtNode = dNode;
        }

        int chosenNode;
        bool partialFromStartOfCurve = false;
        if (endAtNode >= 0)
        {
            if (double.IsInfinity(_dist[endAtNode]))
            {
                return false;
            }

            chosenNode = endAtNode;
        }
        else
        {
            double costC = double.IsInfinity(_dist[cNode]) ? double.PositiveInfinity : _dist[cNode] + distFromStart;
            double costD = double.IsInfinity(_dist[dNode]) ? double.PositiveInfinity : _dist[dNode] + (length - distFromStart);
            if (double.IsInfinity(costC) && double.IsInfinity(costD))
            {
                return false;
            }

            if (costC <= costD)
            {
                chosenNode = cNode;
                partialFromStartOfCurve = true; // walk curve-start → end point (seg1, forward)
            }
            else
            {
                chosenNode = dNode;
                partialFromStartOfCurve = false; // walk curve-end → end point (seg2, reversed)
            }
        }

        if (!TryReconstructEdges(chosenNode, out List<int> edgePath))
        {
            return false;
        }

        List<(Point2d Pt, double OutBulge)> result = AssembleEdges(edgePath);

        if (endAtNode < 0)
        {
            if (!TrySplit(edge.Curve!, onCurve, out Polyline seg1, out Polyline seg2))
            {
                return false;
            }

            try
            {
                List<(Point2d Pt, double OutBulge)> partial = partialFromStartOfCurve
                    ? NSAlignmentCrawlPolylineBuilder.ReadForward(seg1)
                    : NSAlignmentCrawlPolylineBuilder.ReadReversed(seg2);
                NSAlignmentCrawlPolylineBuilder.Append(result, partial);
            }
            finally
            {
                seg1.Dispose();
                seg2.Dispose();
            }
        }

        if (result.Count < 2)
        {
            return false;
        }

        vertices = result;
        return true;
    }

    private bool TryReconstructEdges(int targetNode, out List<int> edgePath)
    {
        edgePath = [];
        int cur = targetNode;
        while (cur != _startNode)
        {
            int prevEdge = _prevEdge[cur];
            if (prevEdge < 0)
            {
                return false;
            }

            edgePath.Add(prevEdge);
            cur = _prevNode[cur];
        }

        edgePath.Reverse();
        return true;
    }

    private void RunDijkstra()
    {
        int count = _net.Nodes.Count;
        _dist = new double[count];
        _prevEdge = new int[count];
        _prevNode = new int[count];
        Array.Fill(_dist, double.PositiveInfinity);
        Array.Fill(_prevEdge, -1);
        Array.Fill(_prevNode, -1);
        _dist[_startNode] = 0.0;

        bool[] settled = new bool[count];
        PriorityQueue<int, double> queue = new();
        queue.Enqueue(_startNode, 0.0);

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            if (settled[u])
            {
                continue;
            }

            settled[u] = true;
            foreach (int edgeIndex in _net.Adjacency[u])
            {
                NetworkEdge edge = _net.Edges[edgeIndex];
                int v = edge.FromNode == u ? edge.ToNode : edge.FromNode;
                double candidate = _dist[u] + edge.Weight;
                if (candidate < _dist[v])
                {
                    _dist[v] = candidate;
                    _prevEdge[v] = edgeIndex;
                    _prevNode[v] = u;
                    queue.Enqueue(v, candidate);
                }
            }
        }
    }

    /// <summary>Walks an edge sequence from the start node, concatenating each edge's geometry
    /// (pipe vertices+bulges, oriented per traversal direction; or a straight block spoke).</summary>
    private List<(Point2d Pt, double OutBulge)> AssembleEdges(List<int> edgePath)
    {
        List<(Point2d Pt, double OutBulge)> result = [];
        int enter = _startNode;
        foreach (int edgeIdx in edgePath)
        {
            NetworkEdge e = _net.Edges[edgeIdx];
            bool forward = e.FromNode == enter;
            int other = forward ? e.ToNode : e.FromNode;

            List<(Point2d Pt, double OutBulge)> segment = e.Kind == EdgeKind.Pipe
                ? (forward
                    ? NSAlignmentCrawlPolylineBuilder.ReadForward(e.Curve!)
                    : NSAlignmentCrawlPolylineBuilder.ReadReversed(e.Curve!))
                : NSAlignmentCrawlPolylineBuilder.Straight(_net.Nodes[enter].Position, _net.Nodes[other].Position);

            NSAlignmentCrawlPolylineBuilder.Append(result, segment);
            enter = other;
        }

        return result;
    }

    /// <summary>The resolved snap of a pick: either a point on a pipe edge, or a graph node.</summary>
    private readonly struct SnapTarget
    {
        public bool OnNode { get; init; }
        public int NodeIndex { get; init; }
        public int EdgeIndex { get; init; }
        public Point3d OnCurve { get; init; }
        public double DistFromStart { get; init; }
    }

    /// <summary>
    /// Snaps a pick to the nearest network feature: the closest point on a pipe, OR a graph node.
    /// A node wins only when it is strictly closer than the nearest pipe point — this is what lets a
    /// pick land on a terminal component port (which no pipe touches) while preserving mid-pipe
    /// snapping everywhere a pipe is actually nearest.
    /// </summary>
    private static bool TryResolveSnap(CrawlNetwork net, Point3d pick, out SnapTarget snap)
    {
        snap = default;
        bool hasPipe = TrySnapToPipe(net, pick, out int edgeIndex, out Point3d onCurve, out double distFromStart);
        bool hasNode = TrySnapToNode(net, pick, out int nodeIndex, out double nodeDist);
        if (!hasPipe && !hasNode)
        {
            return false;
        }

        double pipeDist = hasPipe ? onCurve.DistanceTo(pick) : double.MaxValue;
        snap = hasNode && (!hasPipe || nodeDist < pipeDist)
            ? new SnapTarget { OnNode = true, NodeIndex = nodeIndex }
            : new SnapTarget { OnNode = false, EdgeIndex = edgeIndex, OnCurve = onCurve, DistFromStart = distFromStart };
        return true;
    }

    private static bool TrySnapToNode(CrawlNetwork net, Point3d pick, out int nodeIndex, out double dist)
    {
        nodeIndex = -1;
        dist = double.MaxValue;
        Point2d q = new(pick.X, pick.Y);
        for (int i = 0; i < net.Nodes.Count; i++)
        {
            double d = net.Nodes[i].Position.GetDistanceTo(q);
            if (d < dist)
            {
                dist = d;
                nodeIndex = i;
            }
        }

        return nodeIndex >= 0;
    }

    private static bool TrySnapToPipe(CrawlNetwork net, Point3d pick, out int edgeIndex, out Point3d onCurve, out double distFromStart)
    {
        edgeIndex = -1;
        onCurve = Point3d.Origin;
        distFromStart = 0.0;

        double best = double.MaxValue;
        foreach (NetworkEdge edge in net.Edges)
        {
            if (edge.Removed || edge.Kind != EdgeKind.Pipe || edge.Curve is null)
            {
                continue;
            }

            Point3d cp;
            try
            {
                cp = edge.Curve.GetClosestPointTo(pick, false);
            }
            catch
            {
                continue;
            }

            double d = cp.DistanceTo(pick);
            if (d < best)
            {
                best = d;
                edgeIndex = edge.Index;
                onCurve = cp;
            }
        }

        if (edgeIndex < 0)
        {
            return false;
        }

        try
        {
            distFromStart = net.Edges[edgeIndex].Curve!.GetDistAtPoint(onCurve);
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static bool TrySplit(Polyline curve, Point3d onCurve, out Polyline seg1, out Polyline seg2)
    {
        seg1 = null!;
        seg2 = null!;
        try
        {
            double param = curve.GetParameterAtPoint(onCurve);
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

    private static Point3d Flatten(Point3d p) => new(p.X, p.Y, 0.0);
}
