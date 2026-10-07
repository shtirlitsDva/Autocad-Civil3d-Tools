using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Index.Strtree;

using AcInterval = Autodesk.AutoCAD.Geometry.Interval;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace IntersectUtilities.MPE.AutoAmkB;

/// <summary>A station on an alignment: its plan position and the unit vector pointing to the right of travel.</summary>
internal readonly record struct StationSample(double Station, Point2d Point, Vector2d Right)
{
    public Coordinate At(double offset) =>
        new Coordinate(Point.X + Right.X * offset, Point.Y + Right.Y * offset);

    public Coordinate Origin => new Coordinate(Point.X, Point.Y);
}

internal static class AlignmentSampler
{
    /// <summary>Stations from start to end every <paramref name="step"/> metres, plus the end station.</summary>
    public static Result<IReadOnlyList<StationSample>> Sample(Alignment alignment, double step) =>
        Boundary.Try<IReadOnlyList<StationSample>>(
            () => SampleAll(alignment, step),
            $"Alignment {alignment.Name} kunne ikke gennemløbes");

    private static IReadOnlyList<StationSample> SampleAll(Alignment alignment, double step)
    {
        double start = alignment.StartingStation;
        double end = alignment.EndingStation;
        int count = (int)Math.Floor((end - start) / step + 1e-9);

        List<StationSample> samples = new List<StationSample>(count + 2);
        for (int i = 0; i <= count; i++) samples.Add(At(alignment, start + i * step));
        if (end - (start + count * step) > 1e-6) samples.Add(At(alignment, end));
        return samples;
    }

    // Civil 3D offsets are positive to the right of the alignment direction, so a 1 m offset point
    // gives the right-hand normal without any tangent arithmetic.
    private static StationSample At(Alignment alignment, double station)
    {
        double x = 0, y = 0, xRight = 0, yRight = 0;
        alignment.PointLocation(station, 0.0, ref x, ref y);
        alignment.PointLocation(station, 1.0, ref xRight, ref yRight);
        Vector2d right = new Vector2d(xRight - x, yRight - y);
        return new StationSample(
            station,
            new Point2d(x, y),
            right.Length > 1e-9 ? right.GetNormal() : new Vector2d(0, 0));
    }
}

internal static class CurveCoordinates
{
    /// <summary>
    /// Plan coordinates of a curve: the vertices of a 3D polyline, otherwise points at most
    /// <paramref name="maxStep"/> apart along it (vertices of 2D polylines included, so corners stay sharp).
    /// </summary>
    public static Option<Coordinate[]> Of(Curve curve, Transaction tx, double maxStep) =>
        Boundary.TryOption(() => curve is Polyline3d polyline3d ? Vertices(polyline3d, tx) : Sampled(curve, maxStep))
            .Where(coordinates => coordinates.Length >= 2);

    public static Coordinate[] Sampled(Curve curve, double maxStep)
    {
        double startDistance = curve.GetDistanceAtParameter(curve.StartParam);
        double length = curve.GetDistanceAtParameter(curve.EndParam) - startDistance;
        int count = Math.Max(1, (int)Math.Ceiling(length / maxStep));

        SortedSet<double> distances = new SortedSet<double>();
        for (int i = 0; i < count; i++) distances.Add(length * i / count);
        if (curve is Polyline polyline)
        {
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                distances.Add(polyline.GetDistanceAtParameter(i) - startDistance);
            }
        }

        List<Coordinate> coordinates = distances
            .Where(distance => distance < length - 1e-9)
            .Select(distance => curve.GetPointAtDist(distance))
            .Select(point => new Coordinate(point.X, point.Y))
            .ToList();
        coordinates.Add(new Coordinate(curve.EndPoint.X, curve.EndPoint.Y));
        return coordinates.ToArray();
    }

    private static Coordinate[] Vertices(Polyline3d polyline, Transaction tx)
    {
        List<Coordinate> coordinates = new List<Coordinate>();
        foreach (ObjectId vertexId in polyline)
        {
            PolylineVertex3d vertex = (PolylineVertex3d)tx.GetObject(vertexId, OpenMode.ForRead);
            coordinates.Add(new Coordinate(vertex.Position.X, vertex.Position.Y));
        }
        if (polyline.Closed && coordinates.Count > 0) coordinates.Add(coordinates[0].Copy());
        return coordinates.ToArray();
    }
}

/// <summary>Line segments in a spatial index, each carrying the item it came from.</summary>
internal sealed class SegmentIndex<T>
{
    private readonly STRtree<SegmentItem> _tree = new STRtree<SegmentItem>();

    public int Count { get; private set; }

    public void Add(IReadOnlyList<Coordinate> line, T item)
    {
        for (int i = 0; i + 1 < line.Count; i++)
        {
            LineSegment segment = new LineSegment(line[i], line[i + 1]);
            if (segment.Length < 1e-9) continue;
            _tree.Insert(new Envelope(segment.P0, segment.P1), new SegmentItem(segment, item));
            Count++;
        }
    }

    /// <summary>The item of the first indexed segment the probe segment touches.</summary>
    public Option<T> FirstTouching(Coordinate from, Coordinate to)
    {
        LineSegment probe = new LineSegment(from, to);
        foreach (SegmentItem candidate in _tree.Query(new Envelope(from, to)))
        {
            if (Crossing(probe, candidate.Segment).IsSome()) return Option<T>.Of(candidate.Item);
        }
        return Option<T>.Nothing;
    }

    /// <summary>Distance from <paramref name="from"/> to the nearest indexed segment along the probe, if any.</summary>
    public Option<double> NearestDistanceAlong(Coordinate from, Coordinate to)
    {
        LineSegment probe = new LineSegment(from, to);
        return _tree.Query(new Envelope(from, to))
            .Select(candidate => Crossing(probe, candidate.Segment).Map(point => point.Distance(from)))
            .Values()
            .OrderBy(distance => distance)
            .FirstOption();
    }

    // NetTopologySuite answers "no intersection" with null; this is where that becomes an option.
    private static Option<Coordinate> Crossing(LineSegment a, LineSegment b)
    {
        Coordinate? point = a.Intersection(b);
        return point is null ? Option<Coordinate>.Nothing : Option<Coordinate>.Of(point);
    }

    private sealed record SegmentItem(LineSegment Segment, T Item);
}

/// <summary>Areas in a spatial index, prepared for fast repeated intersection tests.</summary>
internal sealed class AreaIndex<T>
{
    private static readonly GeometryFactory Factory = new GeometryFactory();
    private readonly STRtree<AreaItem> _tree = new STRtree<AreaItem>();

    public int Count { get; private set; }

    public void Add(NtsGeometry area, T item)
    {
        if (area.IsEmpty) return;
        _tree.Insert(area.EnvelopeInternal, new AreaItem(PreparedGeometryFactory.Prepare(area), item));
        Count++;
    }

    /// <summary>The items of every area the segment from <paramref name="from"/> to <paramref name="to"/> touches.</summary>
    public IEnumerable<T> Touching(Coordinate from, Coordinate to)
    {
        NtsGeometry probe = from.Equals2D(to)
            ? Factory.CreatePoint(from)
            : Factory.CreateLineString(new[] { from, to });
        return _tree.Query(new Envelope(from, to))
            .Where(candidate => candidate.Area.Intersects(probe))
            .Select(candidate => candidate.Item);
    }

    private sealed record AreaItem(IPreparedGeometry Area, T Item);
}

internal static class HatchGeometry
{
    private static readonly GeometryFactory Factory = new GeometryFactory();

    /// <summary>
    /// The area a hatch fills, built from its loops with the even-odd rule a normal hatch uses, so an
    /// inner loop is a hole. Loops are read in plan; the DKjord hatches have a +Z normal.
    /// </summary>
    public static Option<NtsGeometry> Of(Hatch hatch) =>
        Boundary.TryOption(() => Build(hatch)).Where(area => !area.IsEmpty);

    private static NtsGeometry Build(Hatch hatch)
    {
        NtsGeometry result = Factory.CreatePolygon();
        for (int i = 0; i < hatch.NumberOfLoops; i++)
        {
            Coordinate[] ring = Ring(hatch.GetLoopAt(i));
            if (ring.Length < 4) continue;
            result = result.SymmetricDifference(Factory.CreatePolygon(ring).Buffer(0));
        }
        return result.Buffer(0);
    }

    private static Coordinate[] Ring(HatchLoop loop)
    {
        List<Coordinate> coordinates = loop.IsPolyline ? PolylineLoop(loop) : CurveLoop(loop);
        if (coordinates.Count > 0 && !coordinates[0].Equals2D(coordinates[^1])) coordinates.Add(coordinates[0].Copy());
        return coordinates.ToArray();
    }

    private static List<Coordinate> PolylineLoop(HatchLoop loop)
    {
        using Polyline polyline = new Polyline();
        int index = 0;
        foreach (BulgeVertex vertex in loop.Polyline)
        {
            polyline.AddVertexAt(index++, vertex.Vertex, vertex.Bulge, 0, 0);
        }
        polyline.Closed = true;
        return CurveCoordinates.Sampled(polyline, 0.5).ToList();
    }

    private static List<Coordinate> CurveLoop(HatchLoop loop)
    {
        const int SamplesPerCurve = 16;
        List<Coordinate> coordinates = new List<Coordinate>();
        foreach (Curve2d curve in loop.Curves)
        {
            AcInterval interval = curve.GetInterval();
            for (int k = 0; k < SamplesPerCurve; k++)
            {
                Point2d point = curve.EvaluatePoint(
                    interval.LowerBound + (interval.UpperBound - interval.LowerBound) * k / SamplesPerCurve);
                coordinates.Add(new Coordinate(point.X, point.Y));
            }
        }
        return coordinates;
    }
}

internal sealed record Stretch<T>(double From, double To, IReadOnlyList<T> Values)
{
    public double Length => To - From;
}

internal static class StretchBuilder
{
    /// <summary>
    /// Joins flagged stations into stretches. Two flagged stations stay in one stretch while the unflagged
    /// gap between them is shorter than <paramref name="joinGap"/>; stretches shorter than
    /// <paramref name="minLength"/> are dropped.
    /// </summary>
    public static IReadOnlyList<Stretch<T>> Build<T>(
        IEnumerable<(double Station, Option<T> Flag)> samples,
        double step,
        double joinGap,
        double minLength)
    {
        List<Stretch<T>> stretches = new List<Stretch<T>>();
        List<T> values = new List<T>();
        double from = 0, to = 0;
        bool open = false;

        foreach ((double station, Option<T> flag) in samples)
        {
            flag.Switch(
                value =>
                {
                    bool continues = open && station - to - step < joinGap - 1e-9;
                    if (!continues)
                    {
                        if (open) stretches.Add(new Stretch<T>(from, to, values));
                        from = station;
                        values = new List<T>();
                        open = true;
                    }
                    to = station;
                    values.Add(value);
                },
                () => { });
        }

        if (open) stretches.Add(new Stretch<T>(from, to, values));
        return stretches.Where(stretch => stretch.Length >= minLength - 1e-9).ToList();
    }
}

internal static class PointClusters
{
    /// <summary>
    /// Groups items whose stations follow each other within <paramref name="distance"/>; a distance of 0
    /// keeps every item on its own.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<T>> ByStation<T>(IEnumerable<T> items, Func<T, double> station, double distance)
    {
        List<List<T>> clusters = new List<List<T>>();
        foreach (T item in items.OrderBy(station))
        {
            bool joins = distance > 0
                && clusters.Count > 0
                && station(item) - station(clusters[^1][^1]) <= distance + 1e-9;
            if (joins) clusters[^1].Add(item);
            else clusters.Add(new List<T> { item });
        }
        return clusters;
    }

    /// <summary>Groups items that lie within <paramref name="distance"/> of any other item in the group.</summary>
    public static IReadOnlyList<IReadOnlyList<T>> ByPosition<T>(IReadOnlyList<T> items, Func<T, Point2d> position, double distance)
    {
        int[] group = Enumerable.Range(0, items.Count).ToArray();
        int Root(int i) => group[i] == i ? i : group[i] = Root(group[i]);

        for (int i = 0; i < items.Count; i++)
        {
            for (int j = i + 1; j < items.Count; j++)
            {
                if (position(items[i]).GetDistanceTo(position(items[j])) <= distance + 1e-9) group[Root(j)] = Root(i);
            }
        }

        return Enumerable.Range(0, items.Count)
            .GroupBy(Root)
            .Select(members => (IReadOnlyList<T>)members.Select(i => items[i]).ToList())
            .ToList();
    }
}
