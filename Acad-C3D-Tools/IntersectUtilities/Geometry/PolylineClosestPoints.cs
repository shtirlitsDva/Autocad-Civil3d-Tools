using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.PolylineProximity;

internal readonly record struct ClosestPolylinePair(Point3d First, Point3d Second)
{
    public double Distance => First.DistanceTo(Second);
    public double Accuracy { get; init; }
}

internal static class PolylineClosestPoints
{
    // Call with non-empty polylines. Their widths are ignored: distance is between paths.
    public static ClosestPolylinePair Find(Polyline first, Polyline second)
    {
        using var firstSegments = new Segments();
        using var secondSegments = new Segments();
        firstSegments.Read(first);
        secondSegments.Read(second);

        double coordinateScale = System.Math.Max(firstSegments.CoordinateScale, secondSegments.CoordinateScale);
        double accuracy = System.Math.Max(Tolerance.Global.EqualPoint, coordinateScale * 1.5e-14);
        var best = new ClosestPolylinePair(first.GetPoint3dAt(0), second.GetPoint3dAt(0)) { Accuracy = accuracy };
        double bestDistance = best.Distance;

        void Consider(Point3d firstPoint, Point3d secondPoint)
        {
            double distance = firstPoint.DistanceTo(secondPoint);
            if (distance < bestDistance)
            {
                best = new ClosestPolylinePair(firstPoint, secondPoint) { Accuracy = accuracy };
                bestDistance = distance;
            }
        }

        foreach (var firstSegment in firstSegments.Curves)
        {
            foreach (var secondSegment in secondSegments.Curves)
            {
                // Box distance is a lower bound. Keep a tolerance margin at the boundary.
                if (BoundsDistance(firstSegment.Bounds, secondSegment.Bounds) >
                    bestDistance + accuracy)
                    continue;

                var pair = firstSegment.Curve.GetClosestPointTo(secondSegment.Curve);
                try
                {
                    // Autodesk returns one point on each bounded curve, in argument order.
                    if (pair.Length != 2)
                        throw new System.InvalidOperationException("AutoCAD did not return a closest-point pair.");
                    Consider(pair[0].Point, pair[1].Point);
                }
                finally
                {
                    foreach (var point in pair)
                        point.Dispose();
                }

                // GE's arc/arc routine can return a local minimum. Chords and their exact
                // sagitta bounds certify the global answer, including non-coplanar arcs.
                if (firstSegment.Radius > 0 || secondSegment.Radius > 0)
                    Refine(firstSegment, secondSegment, accuracy, Consider, () => bestDistance);

                if (bestDistance == 0)
                    return best;
            }
        }

        // Repeated vertices and single-vertex polylines are points, never zero-length GE curves.
        foreach (var point in firstSegments.Points)
        {
            foreach (var segment in secondSegments.Curves)
            {
                using var nearest = segment.Curve.GetClosestPointTo(point);
                Consider(point, nearest.Point);
            }
            foreach (var secondPoint in secondSegments.Points)
                Consider(point, secondPoint);
        }
        foreach (var point in secondSegments.Points)
        {
            foreach (var segment in firstSegments.Curves)
            {
                using var nearest = segment.Curve.GetClosestPointTo(point);
                Consider(nearest.Point, point);
            }
        }
        return best;
    }

    private static void Refine(BoundedCurve first, BoundedCurve second, double accuracy,
        System.Action<Point3d, Point3d> consider, System.Func<double> bestDistance)
    {
        var queue = new System.Collections.Generic.PriorityQueue<PiecePair, double>();
        double circleBound = CircleDistanceBound(first, second);

        void Enqueue(Piece firstPiece, Piece secondPiece)
        {
            var chord = ClosestChords(firstPiece.StartPoint, firstPiece.EndPoint,
                secondPiece.StartPoint, secondPiece.EndPoint);
            double firstFraction = ChordFraction(firstPiece.StartPoint, firstPiece.EndPoint, chord.First);
            double secondFraction = ChordFraction(secondPiece.StartPoint, secondPiece.EndPoint, chord.Second);
            consider(firstPiece.AtFraction(firstFraction), secondPiece.AtFraction(secondFraction));
            double lower = System.Math.Max(circleBound,
                chord.Distance - firstPiece.Deviation - secondPiece.Deviation) - accuracy * 0.25;
            lower = System.Math.Max(0, lower);
            if (lower < bestDistance() - accuracy)
                queue.Enqueue(new PiecePair(firstPiece, secondPiece), lower);
        }

        Enqueue(Piece.Whole(first), Piece.Whole(second));
        while (queue.TryDequeue(out var pair, out double lower))
        {
            if (lower >= bestDistance() - accuracy)
                break;
            if (pair.First.Deviation >= pair.Second.Deviation)
            {
                var (left, right) = pair.First.Split();
                Enqueue(left, pair.Second);
                Enqueue(right, pair.Second);
            }
            else
            {
                var (left, right) = pair.Second.Split();
                Enqueue(pair.First, left);
                Enqueue(pair.First, right);
            }
        }
    }

    private static double ChordFraction(Point3d start, Point3d end, Point3d point)
    {
        Vector3d direction = end - start;
        double squaredLength = direction.DotProduct(direction);
        return squaredLength == 0 ? 0 :
            System.Math.Clamp((point - start).DotProduct(direction) / squaredLength, 0, 1);
    }

    private static ClosestPolylinePair ClosestChords(Point3d firstStart, Point3d firstEnd,
        Point3d secondStart, Point3d secondEnd)
    {
        // Vanishing chords occur on tiny arc pieces; their deviation still bounds the arc.
        if ((firstEnd - firstStart).Length == 0)
            return new ClosestPolylinePair(firstStart, secondStart +
                (secondEnd - secondStart) * ChordFraction(secondStart, secondEnd, firstStart));
        if ((secondEnd - secondStart).Length == 0)
            return new ClosestPolylinePair(firstStart +
                (firstEnd - firstStart) * ChordFraction(firstStart, firstEnd, secondStart), secondStart);

        using var first = new LineSegment3d(firstStart, firstEnd);
        using var second = new LineSegment3d(secondStart, secondEnd);
        var points = first.GetClosestPointTo(second);
        try
        {
            return new ClosestPolylinePair(points[0].Point, points[1].Point);
        }
        finally
        {
            foreach (var point in points)
                point.Dispose();
        }
    }

    private static double CircleDistanceBound(BoundedCurve first, BoundedCurve second)
    {
        if (first.Curve is not CircularArc3d firstArc || second.Curve is not CircularArc3d secondArc)
            return 0;

        // A full circle is a superset of its arc. For tilted planes, rotating the second
        // circle into a parallel plane moves any point by at most radius * normal change.
        Vector3d normal = firstArc.Normal;
        Vector3d otherNormal = secondArc.Normal;
        if (normal.DotProduct(otherNormal) < 0)
            otherNormal = -otherNormal;
        Vector3d centers = secondArc.Center - firstArc.Center;
        double height = centers.DotProduct(normal);
        double separation = (centers - normal * height).Length;
        double planarGap = System.Math.Max(0, System.Math.Max(
            separation - firstArc.Radius - secondArc.Radius,
            System.Math.Abs(firstArc.Radius - secondArc.Radius) - separation));
        return System.Math.Max(0, System.Math.Sqrt(planarGap * planarGap + height * height)
            - secondArc.Radius * (normal - otherNormal).Length);
    }

    private readonly record struct Piece(BoundedCurve Segment, double Start, double End,
        Point3d StartPoint, Point3d EndPoint)
    {
        // 2*sin(angle/4)^2 avoids loss of precision in 1-cos(angle/2).
        public double Deviation => Segment.Radius == 0 ? 0 :
            2 * Segment.Radius * System.Math.Pow(System.Math.Sin((End - Start) * 0.25), 2);

        public Point3d AtFraction(double fraction) => Segment.Curve.EvaluatePoint(Start + (End - Start) * fraction);

        public static Piece Whole(BoundedCurve segment)
        {
            using var interval = segment.Curve.GetInterval();
            return new Piece(segment, interval.LowerBound, interval.UpperBound,
                segment.Curve.StartPoint, segment.Curve.EndPoint);
        }

        public (Piece Left, Piece Right) Split()
        {
            double middle = Start + (End - Start) * 0.5;
            Point3d middlePoint = Segment.Curve.EvaluatePoint(middle);
            return (new Piece(Segment, Start, middle, StartPoint, middlePoint),
                new Piece(Segment, middle, End, middlePoint, EndPoint));
        }
    }

    private readonly record struct PiecePair(Piece First, Piece Second);

    private static double BoundsDistance(Extents3d first, Extents3d second)
    {
        static double Gap(double firstMin, double firstMax, double secondMin, double secondMax) =>
            System.Math.Max(0, System.Math.Max(firstMin - secondMax, secondMin - firstMax));

        double x = Gap(first.MinPoint.X, first.MaxPoint.X, second.MinPoint.X, second.MaxPoint.X);
        double y = Gap(first.MinPoint.Y, first.MaxPoint.Y, second.MinPoint.Y, second.MaxPoint.Y);
        double z = Gap(first.MinPoint.Z, first.MaxPoint.Z, second.MinPoint.Z, second.MaxPoint.Z);
        return System.Math.Sqrt(x * x + y * y + z * z);
    }

    private readonly record struct BoundedCurve(Curve3d Curve, Extents3d Bounds, double Radius);

    private sealed class Segments : System.IDisposable
    {
        public System.Collections.Generic.List<BoundedCurve> Curves { get; } = new();
        public System.Collections.Generic.List<Point3d> Points { get; } = new();
        public double CoordinateScale { get; private set; }

        public void Read(Polyline polyline)
        {
            for (int index = 0; index < polyline.NumberOfVertices; index++)
                CoordinateScale = System.Math.Max(CoordinateScale,
                    (polyline.GetPoint3dAt(index) - Point3d.Origin).Length);
            if (polyline.NumberOfVertices == 1)
            {
                Points.Add(polyline.GetPoint3dAt(0));
                return;
            }

            int count = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
            for (int index = 0; index < count; index++)
            {
                // The final vertex of an open polyline is not a segment.
#pragma warning disable CS8524 // Autodesk's SegmentType enum is exhaustively handled below.
                System.Action add = polyline.GetSegmentType(index) switch
                {
                    SegmentType.Line => () => Add(polyline.GetLineSegmentAt(index)),
                    SegmentType.Arc => () => Add(polyline.GetArcSegmentAt(index)),
                    SegmentType.Coincident or SegmentType.Point or SegmentType.Empty =>
                        () => Points.Add(polyline.GetPoint3dAt(index))
                };
#pragma warning restore CS8524
                add();
            }
        }

        private void Add(Curve3d curve)
        {
            // This is the AutoCAD API/ownership boundary: release a curve if bounding fails.
            try
            {
                using var box = curve.OrthoBoundBlock;
                Curves.Add(new BoundedCurve(curve,
                    new Extents3d(box.GetMinimumPoint(), box.GetMaximumPoint()),
                    curve is CircularArc3d arc ? arc.Radius : 0));
                CoordinateScale = System.Math.Max(CoordinateScale, System.Math.Max(
                    (box.GetMinimumPoint() - Point3d.Origin).Length,
                    (box.GetMaximumPoint() - Point3d.Origin).Length));
            }
            catch
            {
                curve.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            foreach (var segment in Curves)
                segment.Curve.Dispose();
        }
    }
}
