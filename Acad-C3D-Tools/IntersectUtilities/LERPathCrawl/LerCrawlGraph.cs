using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LerPathCrawl;

internal sealed class LerCrawlEdge
{
    public LerCrawlSegment Segment { get; }
    public Polyline Curve { get; }
    public int From { get; }
    public int To { get; }
    public bool Active { get; set; } = true;
    public double Length { get; }
    public Extents3d Bounds { get; }

    public LerCrawlEdge(LerCrawlSegment segment, int from, int to)
    {
        Segment = segment;
        From = from;
        To = to;
        Curve = segment.CreateCurve();
        try
        {
            Length = Curve.Length;
            Bounds = Curve.GeometricExtents;
        }
        catch
        {
            Curve.Dispose();
            throw;
        }
    }
}

internal sealed class LerCrawlGraph : IDisposable
{
    public List<Point2d> Nodes { get; } = new();
    public List<LerCrawlEdge> Edges { get; } = new();
    public List<List<int>> Adjacency { get; } = new();
    private readonly Dictionary<(long X, long Y), List<int>> _cells = new();

    public void Read(IReadOnlyList<LerCrawlSegment> segments)
    {
        foreach (var segment in segments)
        {
            if (segment.Start.GetDistanceTo(segment.End) <= LerCrawlSettings.GeometryTolerance)
                continue;
            AddEdge(segment, Cluster(segment.Start), Cluster(segment.End));
        }

        // An endpoint/vertex lying on another run forms a T-junction. Two crossing
        // interiors do not connect solely because they overlap in the plan view.
        for (int node = 0; node < Nodes.Count; node++)
        {
            Point2d position = Nodes[node];
            Point3d point = new(position.X, position.Y, 0);
            int edgeCount = Edges.Count;
            for (int index = 0; index < edgeCount; index++)
            {
                var edge = Edges[index];
                if (!edge.Active || edge.From == node || edge.To == node || !NearBounds(edge.Bounds, point))
                    continue;
                Point3d nearest = edge.Curve.GetClosestPointTo(point, false);
                if (nearest.DistanceTo(point) > LerCrawlSettings.JoinTolerance)
                    continue;
                double parameter = Math.Clamp(edge.Curve.GetParameterAtPoint(nearest), 0, 1);
                if (parameter * edge.Length <= LerCrawlSettings.JoinTolerance ||
                    (1 - parameter) * edge.Length <= LerCrawlSettings.JoinTolerance)
                    continue;
                Split(index, parameter, node);
            }
        }
        RebuildAdjacency();
    }

    public int AddNode(Point2d point)
    {
        Nodes.Add(point);
        return Nodes.Count - 1;
    }

    public void Split(int edgeIndex, double parameter, int node)
    {
        var edge = Edges[edgeIndex];
        AddEdge(edge.Segment.Slice(edge.Curve, 0, parameter), edge.From, node);
        AddEdge(edge.Segment.Slice(edge.Curve, parameter, 1), node, edge.To);
        edge.Active = false;
    }

    public void RebuildAdjacency()
    {
        Adjacency.Clear();
        for (int node = 0; node < Nodes.Count; node++)
            Adjacency.Add(new List<int>());
        for (int index = 0; index < Edges.Count; index++)
        {
            var edge = Edges[index];
            if (!edge.Active)
                continue;
            Adjacency[edge.From].Add(index);
            Adjacency[edge.To].Add(index);
        }
    }

    public void Dispose()
    {
        foreach (var edge in Edges)
            edge.Curve.Dispose();
    }

    private void AddEdge(LerCrawlSegment segment, int from, int to) => Edges.Add(new LerCrawlEdge(segment, from, to));

    private int Cluster(Point2d point)
    {
        double tolerance = LerCrawlSettings.JoinTolerance;
        long x = (long)Math.Floor(point.X / tolerance);
        long y = (long)Math.Floor(point.Y / tolerance);
        int best = -1;
        double bestDistance = tolerance;
        for (long dx = -1; dx <= 1; dx++)
        for (long dy = -1; dy <= 1; dy++)
        {
            if (!_cells.TryGetValue((x + dx, y + dy), out var candidates))
                continue;
            foreach (int candidate in candidates)
            {
                double distance = Nodes[candidate].GetDistanceTo(point);
                if (distance <= bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }
        if (best >= 0)
            return best;
        int index = AddNode(point);
        if (!_cells.TryGetValue((x, y), out var cell))
        {
            cell = new List<int>();
            _cells.Add((x, y), cell);
        }
        cell.Add(index);
        return index;
    }

    private static bool NearBounds(Extents3d bounds, Point3d point)
    {
        double tolerance = LerCrawlSettings.JoinTolerance;
        return point.X >= bounds.MinPoint.X - tolerance && point.X <= bounds.MaxPoint.X + tolerance &&
            point.Y >= bounds.MinPoint.Y - tolerance && point.Y <= bounds.MaxPoint.Y + tolerance;
    }
}

internal sealed class LerCrawlSession
{
    private readonly LerCrawlGraph _graph;
    private readonly int _start;
    private readonly double[] _distance;
    private readonly int[] _previousEdge;
    private readonly int[] _previousNode;
    public Point2d Start => _graph.Nodes[_start];

    private LerCrawlSession(LerCrawlGraph graph, int start)
    {
        _graph = graph;
        _start = start;
        _distance = new double[graph.Nodes.Count];
        _previousEdge = new int[graph.Nodes.Count];
        _previousNode = new int[graph.Nodes.Count];
        Array.Fill(_distance, double.PositiveInfinity);
        Array.Fill(_previousEdge, -1);
        Array.Fill(_previousNode, -1);
        _distance[start] = 0;
        var queue = new PriorityQueue<int, double>();
        queue.Enqueue(start, 0);
        while (queue.TryDequeue(out int node, out double cost))
        {
            if (cost > _distance[node])
                continue;
            foreach (int index in graph.Adjacency[node])
            {
                var edge = graph.Edges[index];
                int next = edge.From == node ? edge.To : edge.From;
                double candidate = cost + edge.Length;
                if (candidate >= _distance[next])
                    continue;
                _distance[next] = candidate;
                _previousEdge[next] = index;
                _previousNode[next] = node;
                queue.Enqueue(next, candidate);
            }
        }
    }

    public static LerCrawlResult<LerCrawlStart> Create(LerCrawlGraph graph, ObjectId selectedId, Point3d pick)
    {
        return Snap(graph, pick, edge => edge.Segment.SourceId == selectedId).Match(
            snap =>
            {
                var edge = graph.Edges[snap.Edge];
                int start;
                if (snap.Parameter * edge.Length <= LerCrawlSettings.GeometryTolerance)
                    start = edge.From;
                else if ((1 - snap.Parameter) * edge.Length <= LerCrawlSettings.GeometryTolerance)
                    start = edge.To;
                else
                {
                    start = graph.AddNode(snap.Point);
                    graph.Split(snap.Edge, snap.Parameter, start);
                }
                graph.RebuildAdjacency();
                return LerCrawlResult<LerCrawlStart>.Ok(new(new LerCrawlSession(graph, start)));
            },
            error => LerCrawlResult<LerCrawlStart>.Fault(error));
    }

    public LerCrawlResult<LerCrawlRoute> Route(Point3d pick) => Snap(_graph, pick, _ => true).Match(
        snap =>
        {
            var edge = _graph.Edges[snap.Edge];
            double fromCost = _distance[edge.From] + snap.Parameter * edge.Length;
            double toCost = _distance[edge.To] + (1 - snap.Parameter) * edge.Length;
            if (double.IsPositiveInfinity(fromCost) && double.IsPositiveInfinity(toCost))
                return LerCrawlResult<LerCrawlRoute>.Fault("No connected route on this LER layer to the endpoint.");
            bool forward = fromCost <= toCost;
            int node = forward ? edge.From : edge.To;
            var segments = new List<LerCrawlSegment>();
            while (node != _start)
            {
                int index = _previousEdge[node];
                var previous = _graph.Edges[index];
                segments.Add(previous.To == node ? previous.Segment : previous.Segment.Reverse());
                node = _previousNode[node];
            }
            segments.Reverse();
            if (forward && snap.Parameter * edge.Length > LerCrawlSettings.GeometryTolerance)
                segments.Add(edge.Segment.Slice(edge.Curve, 0, snap.Parameter));
            else if (!forward && (1 - snap.Parameter) * edge.Length > LerCrawlSettings.GeometryTolerance)
                segments.Add(edge.Segment.Slice(edge.Curve, 1, snap.Parameter));
            return segments.Count == 0
                ? LerCrawlResult<LerCrawlRoute>.Fault("The start and endpoint are the same.")
                : LerCrawlResult<LerCrawlRoute>.Ok(new(segments));
        },
        error => LerCrawlResult<LerCrawlRoute>.Fault(error));

    private readonly record struct SnapPoint(int Edge, Point2d Point, double Parameter);

    private static LerCrawlResult<SnapPoint> Snap(LerCrawlGraph graph, Point3d pick, Func<LerCrawlEdge, bool> allowed)
    {
        pick = new Point3d(pick.X, pick.Y, 0);
        int best = -1;
        double bestDistance = double.PositiveInfinity;
        Point3d bestPoint = Point3d.Origin;
        for (int index = 0; index < graph.Edges.Count; index++)
        {
            var edge = graph.Edges[index];
            if (!edge.Active || !allowed(edge))
                continue;
            double x = Math.Max(0, Math.Max(edge.Bounds.MinPoint.X - pick.X, pick.X - edge.Bounds.MaxPoint.X));
            double y = Math.Max(0, Math.Max(edge.Bounds.MinPoint.Y - pick.Y, pick.Y - edge.Bounds.MaxPoint.Y));
            if (Math.Sqrt(x * x + y * y) > bestDistance + LerCrawlSettings.GeometryTolerance)
                continue;
            Point3d point = edge.Curve.GetClosestPointTo(pick, false);
            double distance = point.DistanceTo(pick);
            if (distance < bestDistance)
            {
                best = index;
                bestDistance = distance;
                bestPoint = point;
            }
        }
        if (best < 0)
            return LerCrawlResult<SnapPoint>.Fault("No usable polyline segments found on the selected LER layer.");
        double parameter = Math.Clamp(graph.Edges[best].Curve.GetParameterAtPoint(bestPoint), 0, 1);
        return LerCrawlResult<SnapPoint>.Ok(new(best, new Point2d(bestPoint.X, bestPoint.Y), parameter));
    }
}
