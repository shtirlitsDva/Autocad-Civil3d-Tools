using System;
using System.Collections.Generic;
using System.Linq;
using IntersectUtilities.LongitudinalProfiles;

namespace IntersectUtilities.LongitudinalProfiles.Baseline;

internal readonly record struct BaselinePoint(double X, double Y)
{
    internal bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
    internal double Length => Math.Sqrt(X * X + Y * Y);
    public static BaselinePoint operator +(BaselinePoint a, BaselinePoint b) => new(a.X + b.X, a.Y + b.Y);
    public static BaselinePoint operator -(BaselinePoint a, BaselinePoint b) => new(a.X - b.X, a.Y - b.Y);
    public static BaselinePoint operator *(BaselinePoint a, double scale) => new(a.X * scale, a.Y * scale);
}

internal readonly record struct BaselineVertex(BaselinePoint Point, double Bulge);

// Radius is the requested setting; each arc carries its actual constructed radius.
internal sealed record BaselineSolution(
    BaselinePoint Origin, BaselinePoint EndPoint, IReadOnlyList<BaselineSegment> Segments, double Radius, int Crests, int Sags)
{
    internal IReadOnlyList<BaselineVertex> Vertices => Segments
        .Select(segment => new BaselineVertex(segment.Start + Origin, segment.Bulge))
        .Append(new BaselineVertex(EndPoint, 0)).ToArray();
}

// Only the two nested cases can extend this type. Consumers use the operations,
// so a new case must implement every geometric check rather than being ignored.
internal abstract record BaselineSegment
{
    private BaselineSegment(BaselinePoint start, BaselinePoint end, int sourceVertex)
    {
        Start = start;
        End = end;
        SourceVertex = sourceVertex;
    }
    internal BaselinePoint Start { get; }
    internal BaselinePoint End { get; }
    internal int SourceVertex { get; }
    internal abstract double Bulge { get; }
    internal abstract double StartDirection { get; }
    internal abstract double EndDirection { get; }
    internal abstract double HeightAt(double x);
    internal abstract double DirectionAt(double x);
    internal abstract BaselineSegment Slice(double left, double right);
    internal abstract BaselineSegment Translate(BaselinePoint offset);
    internal abstract double MaximumExcess(BaselinePoint guideStart, BaselinePoint guideEnd, double left, double right);

    internal sealed record Line(BaselinePoint From, BaselinePoint To, int Vertex)
        : BaselineSegment(From, To, Vertex)
    {
        internal override double Bulge => 0;
        internal override double StartDirection => Math.Atan2(End.Y - Start.Y, End.X - Start.X);
        internal override double EndDirection => StartDirection;
        internal override double HeightAt(double x) => Start.Y + (End.Y - Start.Y) * ((x - Start.X) / (End.X - Start.X));
        internal override double DirectionAt(double x) => StartDirection;
        internal override BaselineSegment Slice(double left, double right) => new Line(
            new(left, HeightAt(left)), new(right, HeightAt(right)), SourceVertex);
        internal override BaselineSegment Translate(BaselinePoint offset) => new Line(Start + offset, End + offset, SourceVertex);
        internal override double MaximumExcess(BaselinePoint a, BaselinePoint b, double left, double right)
            => Math.Max(Excess(left, a, b), Excess(right, a, b));
        private double Excess(double x, BaselinePoint a, BaselinePoint b)
            => HeightAt(x) - GuideHeight(a, b, x);
    }

    internal sealed record Arc(
        BaselinePoint From, BaselinePoint To, BaselinePoint Center, double Radius,
        int Curvature, double Incoming, double Outgoing, int Vertex)
        : BaselineSegment(From, To, Vertex)
    {
        internal override double Bulge => Math.Tan((Outgoing - Incoming) / 4);
        internal override double StartDirection => Incoming;
        internal override double EndDirection => Outgoing;
        internal override double HeightAt(double x)
        {
            if (x == Start.X) return Start.Y;
            // Anchor the circle height to its known endpoint. Subtracting a
            // huge centre elevation from a huge radius loses near-straight arcs.
            double first = (Start.X - Center.X) / Radius;
            double current = (x - Center.X) / Radius;
            double denominator = Math.Sqrt(Math.Max(0, (1 - first) * (1 + first))) +
                Math.Sqrt(Math.Max(0, (1 - current) * (1 + current)));
            return denominator > 0 ? Start.Y + Curvature * (x - Start.X) * (first + current) / denominator : double.NaN;
        }
        internal override double DirectionAt(double x) => Math.Asin(Math.Clamp(Curvature * (x - Center.X) / Radius, -1, 1));
        internal override BaselineSegment Slice(double left, double right) => new Arc(
            new(left, HeightAt(left)), new(right, HeightAt(right)), Center, Radius, Curvature,
            DirectionAt(left), DirectionAt(right), SourceVertex);
        internal override BaselineSegment Translate(BaselinePoint offset) => new Arc(
            Start + offset, End + offset, Center + offset, Radius, Curvature, Incoming, Outgoing, SourceVertex);
        internal override double MaximumExcess(BaselinePoint a, BaselinePoint b, double left, double right)
        {
            double maximum = Math.Max(Excess(left), Excess(right));
            // The only interior extremum of circle height minus a straight guide
            // occurs where the circle tangent has the guide's slope.
            double angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
            double stationary = Center.X + Curvature * Radius * Math.Sin(angle);
            if (stationary > left && stationary < right)
                maximum = Math.Max(maximum, Excess(stationary));
            return maximum;

            double Excess(double x) => HeightAt(x) - GuideHeight(a, b, x);
        }
    }

    private static double GuideHeight(BaselinePoint a, BaselinePoint b, double x)
        => a.Y + (b.Y - a.Y) * ((x - a.X) / (b.X - a.X));
}

internal sealed record BaselineProblem(int FirstVertex, int LastVertex, BaselinePoint From, BaselinePoint To, string Reason);
internal sealed record BaselinePlan(IReadOnlyList<BaselineSolution> Parts, IReadOnlyList<BaselineProblem> Problems)
{
    internal IReadOnlyList<int> MinimumVertices { get; init; } = Array.Empty<int>();
}

internal static class ProfileBaselineGeometry
{
    internal const double LinearTolerance = 1e-8;
    private const double AngularTolerance = 1e-12;
    private readonly record struct GuideVertex(BaselinePoint Point, int Index);
    private readonly record struct Circle(BaselinePoint Center, double SignedRadius, double Incoming,
        double Outgoing, BaselinePoint GuidePoint, int Index, bool FitsAdjacentEdges);
    private readonly record struct Connection(BaselinePoint Start, BaselinePoint End, double Angle);
    private readonly record struct Gap(double Left, double Right);

    internal static Result<BaselineSolution> Solve(IReadOnlyList<BaselinePoint> input, double radius)
        => Analyze(input, radius).Match(plan => plan.Problems.Count == 0 && plan.Parts.Count == 1
            ? Result<BaselineSolution>.Success(plan.Parts[0])
            : Result<BaselineSolution>.Failure(string.Join(" ", plan.Problems.Select(p =>
                p.FirstVertex == p.LastVertex ? $"Draft vertex {p.FirstVertex}: {p.Reason}"
                    : $"Draft vertices {p.FirstVertex}-{p.LastVertex}: {p.Reason}"))),
            Result<BaselineSolution>.Failure);

    internal static Result<BaselinePlan> Analyze(IReadOnlyList<BaselinePoint> input, double radius)
        => AnalyzeCore(input, radius, radius, new HashSet<int>());

    internal static Result<BaselinePlan> Analyze(IReadOnlyList<BaselinePoint> input, double desiredRadius, double minimumRadius)
        => Analyze(input, desiredRadius).Bind(first => RetryWithMinimum(input, desiredRadius, minimumRadius, first));

    internal static Result<BaselinePlan> RetryWithMinimum(IReadOnlyList<BaselinePoint> input, double desiredRadius,
        double minimumRadius, BaselinePlan first)
    {
        if (!double.IsFinite(minimumRadius) || minimumRadius <= LinearTolerance || minimumRadius > desiredRadius)
            return Result<BaselinePlan>.Failure("Minimum radius must be finite, positive and no greater than the desired radius.");
        if (first.Problems.Count == 0 || minimumRadius == desiredRadius)
            return Result<BaselinePlan>.Success(first);
        var failedVertices = first.Problems.SelectMany(problem => new[] { problem.FirstVertex, problem.LastVertex })
            .Where(index => index > 1 && index < input.Count).ToHashSet();
        if (failedVertices.Count == 0) return Result<BaselinePlan>.Success(first);
        // Reuse the already calculated desired-radius result. There is exactly
        // one local fallback pass, followed by the normal anyway/abort workflow.
        return AnalyzeCore(input, desiredRadius, minimumRadius, failedVertices)
            .Map(final => final with { MinimumVertices = failedVertices.OrderBy(index => index).ToArray() });
    }

    private static Result<BaselinePlan> AnalyzeCore(IReadOnlyList<BaselinePoint> input, double radius,
        double minimumRadius, IReadOnlySet<int> minimumVertices)
    {
        if (!double.IsFinite(radius) || radius <= LinearTolerance)
            return Result<BaselinePlan>.Failure("Radius must be finite and greater than 0.00000001 m.");
        if (input.Count < 2 || input.Any(point => !point.IsFinite))
            return Result<BaselinePlan>.Failure("The guide needs at least two finite vertices.");

        var original = input.Select((point, index) => new GuideVertex(point, index + 1)).ToList();
        if (original[^1].Point.X < original[0].Point.X) original.Reverse();
        var origin = original[0].Point;
        var endPoint = original[^1].Point;
        original = original.Select(vertex => vertex with { Point = vertex.Point - origin }).ToList();
        for (int i = 1; i < original.Count; i++)
            if (!(original[i].Point - original[i - 1].Point).IsFinite ||
                original[i].Point.X - original[i - 1].Point.X <= LinearTolerance)
                return Result<BaselinePlan>.Failure("The guide must progress consistently in X without vertical, duplicate or backtracking edges.");

        var guide = new List<GuideVertex>();
        foreach (var vertex in original)
        {
            guide.Add(vertex);
            while (guide.Count >= 3 && Math.Abs(Direction(guide[^2].Point, guide[^1].Point) -
                Direction(guide[^3].Point, guide[^2].Point)) <= AngularTolerance)
                guide.RemoveAt(guide.Count - 2);
        }

        var cover = original.Select(v => v.Point).ToArray();
        var circles = new List<Result<Circle>>
        {
            Result<Circle>.Success(new(guide[0].Point, 0, 0, 0, guide[0].Point, guide[0].Index, true))
        };
        var problems = new List<BaselineProblem>();
        var gaps = new List<Gap>();
        void Problem(int first, int last, string reason)
            => problems.Add(new(guide[first].Index, guide[last].Index,
                guide[first].Point + origin, guide[last].Point + origin, reason));
        for (int i = 1; i < guide.Count - 1; i++)
        {
            int node = i;
            double localRadius = minimumVertices.Contains(guide[i].Index) ? minimumRadius : radius;
            Result<Circle> circle = MakeCircle(guide[i - 1], guide[i], guide[i + 1], localRadius);
            circle.Match(value =>
            {
                if (!value.FitsAdjacentEdges)
                {
                    Problem(node, node, "The crest radius extends beyond its adjacent draft edges.");
                    gaps.Add(CircleGap(value, guide[node - 1].Point.X, guide[node + 1].Point.X));
                }
                return 0;
            }, reason =>
            {
                Problem(node, node, reason);
                gaps.Add(new(guide[node - 1].Point.X, guide[node + 1].Point.X));
                return 0;
            });
            circles.Add(circle);
        }
        circles.Add(Result<Circle>.Success(new(guide[^1].Point, 0, 0, 0, guide[^1].Point, guide[^1].Index, true)));

        var connections = new List<Result<Connection>>();
        for (int i = 0; i < circles.Count - 1; i++)
        {
            int edge = i;
            bool bothCircles = circles[i].Match(c => c.FitsAdjacentEdges, _ => false) && circles[i + 1].Match(c => c.FitsAdjacentEdges, _ => false);
            var connection = circles[i].Bind(first => circles[edge + 1].Bind(second =>
                MakeConnection(first, second, guide[edge].Point.X, guide[edge + 1].Point.X, cover)));
            connection.Match(_ => 0, reason =>
            {
                if (bothCircles) Problem(edge, edge + 1, reason);
                var gap = circles[edge].Match(first => circles[edge + 1].Match(second =>
                    ConnectionGap(first, second, guide[edge].Point.X, guide[edge + 1].Point.X),
                    _ => new Gap(guide[edge].Point.X, guide[edge + 1].Point.X)),
                    _ => new Gap(guide[edge].Point.X, guide[edge + 1].Point.X));
                gaps.Add(gap);
                return 0;
            });
            connections.Add(connection);
        }

        var pieces = new List<BaselineSegment>();
        for (int i = 0; i < connections.Count; i++)
        {
            int edge = i;
            connections[i].Match(connection =>
            {
                pieces.Add(new BaselineSegment.Line(connection.Start, connection.End, guide[edge + 1].Index));
                return 0;
            }, _ => 0);
            if (i == connections.Count - 1) continue;
            int node = i + 1;
            circles[node].Match(circle =>
            {
                if (!circle.FitsAdjacentEdges) return 0;
                double incoming = connections[node - 1].Match(c => c.Angle, _ => circle.Incoming);
                double outgoing = connections[node].Match(c => c.Angle, _ => circle.Outgoing);
                MakeArc(circle, incoming, outgoing, guide[node - 1].Point.X, guide[node + 1].Point.X, cover)
                    .Match(arc => { pieces.Add(arc); return 0; }, reason =>
                    {
                        Problem(node, node, reason);
                        gaps.Add(CircleGap(circle, guide[node - 1].Point.X, guide[node + 1].Point.X));
                        return 0;
                    });
                return 0;
            }, _ => 0);
        }

        var mergedGaps = new List<Gap>();
        foreach (var gap in gaps.OrderBy(g => g.Left))
        {
            if (mergedGaps.Count > 0 && gap.Left <= mergedGaps[^1].Right + LinearTolerance)
                mergedGaps[^1] = new(mergedGaps[^1].Left, Math.Max(mergedGaps[^1].Right, gap.Right));
            else mergedGaps.Add(gap);
        }
        var retained = new List<BaselineSegment>();
        foreach (var piece in pieces)
        {
            double cursor = piece.Start.X;
            foreach (var gap in mergedGaps)
            {
                if (gap.Right <= cursor || gap.Left >= piece.End.X) continue;
                if (gap.Left > cursor + LinearTolerance)
                    retained.Add(Crop(piece, cursor, Math.Min(gap.Left, piece.End.X)));
                cursor = Math.Max(cursor, gap.Right);
                if (cursor >= piece.End.X) break;
            }
            if (piece.End.X - cursor > LinearTolerance)
                retained.Add(Crop(piece, cursor, piece.End.X));
        }

        var runs = new List<List<BaselineSegment>>();
        foreach (var piece in retained)
        {
            if (runs.Count > 0 && (runs[^1][^1].End - piece.Start).Length <= LinearTolerance &&
                Math.Abs(runs[^1][^1].EndDirection - piece.StartDirection) > 1e-8)
                return Result<BaselinePlan>.Failure("The tangent join cannot be represented at drawing precision.");
            if (runs.Count == 0 || (runs[^1][^1].End - piece.Start).Length > LinearTolerance)
                runs.Add(new());
            runs[^1].Add(piece);
        }
        var parts = new List<BaselineSolution>();
        foreach (var run in runs)
        {
            string failure = ValidateCover(run, cover).Match(_ => string.Empty, reason => reason);
            if (failure.Length > 0)
                return Result<BaselinePlan>.Failure("Could not retain the partial geometry at drawing precision. The input must be kept.");
            var last = run[^1].End + origin;
            if ((last - endPoint).Length <= LinearTolerance) last = endPoint;
            parts.Add(new(origin, last, run.AsReadOnly(), radius, run.Count(p => p.Bulge < 0), run.Count(p => p.Bulge > 0)));
        }
        if (problems.Count == 0 && parts.Count != 1)
            return Result<BaselinePlan>.Failure("Could not construct one continuous baseline at drawing precision.");
        return Result<BaselinePlan>.Success(new(parts.AsReadOnly(), problems.AsReadOnly()));
    }

    private static Result<Circle> MakeCircle(GuideVertex first, GuideVertex vertex, GuideVertex last, double radius)
    {
        double incoming = Direction(first.Point, vertex.Point), outgoing = Direction(vertex.Point, last.Point);
        double change = outgoing - incoming, mean = (incoming + outgoing) / 2;
        bool sag = change > 0;
        var center = vertex.Point + Normal(mean) * (sag ? radius : -radius / Math.Cos(change / 2));
        if (!center.IsFinite) return Result<Circle>.Failure("The circle cannot be represented at this radius.");
        bool fits = true;
        if (!sag)
        {
            var start = center + Normal(incoming) * radius;
            var end = center + Normal(outgoing) * radius;
            if (start.X < first.Point.X - LinearTolerance || end.X > last.Point.X + LinearTolerance)
                fits = false;
        }
        return Result<Circle>.Success(new(center, sag ? radius : -radius, incoming, outgoing, vertex.Point, vertex.Index, fits));
    }

    private static Gap CircleGap(Circle circle, double leftLimit, double rightLimit)
    {
        var first = circle.Center - Normal(circle.Incoming) * circle.SignedRadius;
        var last = circle.Center - Normal(circle.Outgoing) * circle.SignedRadius;
        double left = Math.Max(leftLimit, Math.Min(first.X, circle.GuidePoint.X));
        double right = Math.Min(rightLimit, Math.Max(last.X, circle.GuidePoint.X));
        return right - left > LinearTolerance ? new(left, right) : new(leftLimit, rightLimit);
    }

    private static Gap ConnectionGap(Circle first, Circle second, double leftLimit, double rightLimit)
    {
        var start = first.Center - Normal(first.Outgoing) * first.SignedRadius;
        var end = second.Center - Normal(second.Incoming) * second.SignedRadius;
        double left = Math.Max(leftLimit, Math.Min(start.X, end.X));
        double right = Math.Min(rightLimit, Math.Max(start.X, end.X));
        return right - left > LinearTolerance ? new(left, right) : new(leftLimit, rightLimit);
    }

    private static Result<Connection> MakeConnection(Circle first, Circle second, double left, double right,
        IReadOnlyList<BaselinePoint> guide)
    {
        var delta = second.Center - first.Center;
        double distance = delta.Length, separation = second.SignedRadius - first.SignedRadius;
        if (!double.IsFinite(distance) || distance <= Math.Abs(separation) + LinearTolerance)
            return Result<Connection>.Failure("No forward common tangent fits this radius.");
        double angle = Math.Atan2(delta.Y, delta.X) - Math.Asin(Math.Clamp(separation / distance, -1, 1));
        if (Math.Abs(angle) >= Math.PI / 2 - AngularTolerance)
            return Result<Connection>.Failure("The tangent would be vertical or run backwards.");
        var start = first.Center - Normal(angle) * first.SignedRadius;
        var end = second.Center - Normal(angle) * second.SignedRadius;
        if (!start.IsFinite || !end.IsFinite || end.X - start.X <= LinearTolerance ||
            start.X < left - LinearTolerance || end.X > right + LinearTolerance)
            return Result<Connection>.Failure("The tangent does not fit between its adjacent draft vertices.");
        var line = new BaselineSegment.Line(start, end, second.Index);
        return ValidateCover(new[] { line }, guide).Match(_ => Result<Connection>.Success(new(start, end, angle)),
            _ => Result<Connection>.Failure("The tangent would rise above the guide."));
    }

    private static Result<BaselineSegment> MakeArc(Circle circle, double first, double last, double left, double right,
        IReadOnlyList<BaselinePoint> guide)
    {
        int curvature = Math.Sign(circle.SignedRadius);
        if (curvature * (last - first) <= AngularTolerance)
            return Result<BaselineSegment>.Failure("Neighbouring tangencies overlap or reverse the intended arc.");
        bool Contains(double angle) => curvature * (angle - first) >= -AngularTolerance &&
            curvature * (last - angle) >= -AngularTolerance;
        if (curvature > 0 ? !Contains((circle.Incoming + circle.Outgoing) / 2)
            : !Contains(circle.Incoming) || !Contains(circle.Outgoing))
            return Result<BaselineSegment>.Failure("The trimmed arc would lose its required draft contact.");
        var start = circle.Center - Normal(first) * circle.SignedRadius;
        var end = circle.Center - Normal(last) * circle.SignedRadius;
        var arc = new BaselineSegment.Arc(start, end, circle.Center, Math.Abs(circle.SignedRadius), curvature, first, last, circle.Index);
        if (!start.IsFinite || !end.IsFinite || end.X - start.X <= LinearTolerance ||
            start.X < left - LinearTolerance || end.X > right + LinearTolerance ||
            !double.IsFinite(arc.Bulge) || Math.Abs(arc.HeightAt(end.X) - end.Y) > LinearTolerance)
            return Result<BaselineSegment>.Failure("The arc is too large, small, steep or numerically unstable for its adjacent edges.");
        return ValidateCover(new[] { arc }, guide).Match(_ => Result<BaselineSegment>.Success(arc),
            _ => Result<BaselineSegment>.Failure("The arc would rise above the guide."));
    }

    private static BaselineSegment Crop(BaselineSegment piece, double left, double right)
        => left == piece.Start.X && right == piece.End.X ? piece : piece.Slice(left, right);

    internal static Result<Unit> ValidateCover(IReadOnlyList<BaselineSegment> segments, IReadOnlyList<BaselinePoint> guide)
    {
        int firstGuideEdge = 0;
        foreach (var segment in segments)
        {
            if (segment.Start.X < guide[0].X - LinearTolerance || segment.End.X > guide[^1].X + LinearTolerance)
                return Result<Unit>.Failure($"Vertex {segment.SourceVertex}: result extends outside the guide.");
            while (firstGuideEdge < guide.Count - 2 && guide[firstGuideEdge + 1].X < segment.Start.X) firstGuideEdge++;
            for (int i = firstGuideEdge; i < guide.Count - 1 && guide[i].X <= segment.End.X; i++)
            {
                double left = Math.Max(segment.Start.X, guide[i].X), right = Math.Min(segment.End.X, guide[i + 1].X);
                if (right < left) continue;
                double excess = segment.MaximumExcess(guide[i], guide[i + 1], left, right);
                if (!double.IsFinite(excess) || excess > LinearTolerance)
                    return Result<Unit>.Failure($"Vertex {segment.SourceVertex}: the result would rise above draft edge {i + 1}.");
            }
        }
        return Result<Unit>.Success(Unit.Value);
    }
    private static double Direction(BaselinePoint a, BaselinePoint b) => Math.Atan2(b.Y - a.Y, b.X - a.X);
    private static BaselinePoint Normal(double angle) => new(-Math.Sin(angle), Math.Cos(angle));
}
