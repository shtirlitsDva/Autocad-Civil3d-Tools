using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanSolver
{
    private const double DistanceTolerance = 1e-6;
    private const double AngleTolerance = 1e-6;

    public PipePlanAnalysis Analyze(IReadOnlyList<Point3d> points, double radius)
    {
        if (points.Count < 2)
        {
            return PipePlanAnalysis.Raw(points, true, "Pick at least two points.");
        }

        if (radius <= DistanceTolerance)
        {
            return PipePlanAnalysis.Invalid(points, "Radius must be greater than zero.");
        }

        int bendCount = points.Count;
        BendInfo?[] bends = new BendInfo?[bendCount];

        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector2d incoming = To2D(points[i] - points[i - 1]);
            Vector2d outgoing = To2D(points[i + 1] - points[i]);

            double incomingLength = incoming.Length;
            double outgoingLength = outgoing.Length;
            if (incomingLength <= DistanceTolerance || outgoingLength <= DistanceTolerance)
            {
                return PipePlanAnalysis.Invalid(points, "Consecutive points must be separated.");
            }

            Vector2d u = incoming / incomingLength;
            Vector2d v = outgoing / outgoingLength;

            double dot = Math.Clamp(u.DotProduct(v), -1.0, 1.0);
            double deflection = Math.Acos(dot);

            if (deflection <= AngleTolerance)
            {
                continue;
            }

            if (Math.Abs(Math.PI - deflection) <= AngleTolerance)
            {
                return PipePlanAnalysis.Invalid(points, "A 180 degree reversal cannot be solved with a finite bend.");
            }

            double tangentLength = radius * Math.Tan(deflection / 2.0);
            if (!double.IsFinite(tangentLength))
            {
                return PipePlanAnalysis.Invalid(points, "The selected radius cannot be solved for this turn.");
            }

            double cross = (u.X * v.Y) - (u.Y * v.X);
            int sign = cross >= 0.0 ? 1 : -1;

            bends[i] = new BendInfo(points[i], u, v, tangentLength, deflection, sign);
        }

        for (int segmentIndex = 0; segmentIndex < points.Count - 1; segmentIndex++)
        {
            double length = points[segmentIndex].DistanceTo(points[segmentIndex + 1]);
            double trimStart = bends[segmentIndex]?.TangentLength ?? 0.0;
            double trimEnd = bends[segmentIndex + 1]?.TangentLength ?? 0.0;

            if ((trimStart + trimEnd) - length > DistanceTolerance)
            {
                string message = $"Segment {segmentIndex + 1} is too short for the selected radius.";
                return PipePlanAnalysis.Invalid(points, message);
            }
        }

        List<PolylineVertexData> vertexData = [new PolylineVertexData(To2D(points[0]), 0.0)];
        List<PipePlanRadiusAnnotation> radiusAnnotations = [];
        List<PipePlanFilletEndpointMarker> filletEndpointMarkers = [];
        for (int i = 1; i < points.Count - 1; i++)
        {
            BendInfo? bend = bends[i];
            if (bend is null)
            {
                continue;
            }

            Point2d tangentIn = To2D(points[i]) - (bend.IncomingDirection * bend.TangentLength);
            Point2d tangentOut = To2D(points[i]) + (bend.OutgoingDirection * bend.TangentLength);
            double bulge = bend.Sign * Math.Tan(bend.Deflection / 4.0);

            AppendVertex(vertexData, tangentIn, bulge);
            AppendVertex(vertexData, tangentOut, 0.0);
            radiusAnnotations.Add(CreateRadiusAnnotation(tangentIn, bend, radius));
            filletEndpointMarkers.Add(new PipePlanFilletEndpointMarker(
                new Point3d(tangentIn.X, tangentIn.Y, bend.Point.Z),
                new Point3d(tangentOut.X, tangentOut.Y, bend.Point.Z)));
        }

        AppendVertex(vertexData, To2D(points[^1]), 0.0);

        return PipePlanAnalysis.Curved(points, vertexData, radiusAnnotations, filletEndpointMarkers, "Current draft is feasible.");
    }

    private static void AppendVertex(List<PolylineVertexData> vertices, Point2d point, double bulge)
    {
        if (vertices.Count == 0)
        {
            vertices.Add(new PolylineVertexData(point, bulge));
            return;
        }

        PolylineVertexData last = vertices[^1];
        if (last.Point.GetDistanceTo(point) <= DistanceTolerance)
        {
            vertices[^1] = last with { Bulge = bulge };
            return;
        }

        vertices.Add(new PolylineVertexData(point, bulge));
    }

    private static Point2d To2D(Point3d point)
    {
        return new Point2d(point.X, point.Y);
    }

    private static Vector2d To2D(Vector3d vector)
    {
        return new Vector2d(vector.X, vector.Y);
    }

    private static PipePlanRadiusAnnotation CreateRadiusAnnotation(Point2d tangentIn, BendInfo bend, double radius)
    {
        Vector2d offsetToCenter = RotateLeft(bend.IncomingDirection) * (bend.Sign * radius);
        Point2d center = tangentIn + offsetToCenter;

        Vector2d startRadius = tangentIn - center;
        Vector2d midRadius = Rotate(startRadius, bend.Sign * bend.Deflection / 2.0);
        Point2d arcMidPoint = center + midRadius;

        return new PipePlanRadiusAnnotation(
            new Point3d(center.X, center.Y, bend.Point.Z),
            new Point3d(arcMidPoint.X, arcMidPoint.Y, bend.Point.Z),
            radius);
    }

    private static Vector2d RotateLeft(Vector2d vector)
    {
        return new Vector2d(-vector.Y, vector.X);
    }

    private static Vector2d Rotate(Vector2d vector, double angle)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        return new Vector2d(
            (vector.X * cos) - (vector.Y * sin),
            (vector.X * sin) + (vector.Y * cos));
    }

    private sealed record BendInfo(
        Point3d Point,
        Vector2d IncomingDirection,
        Vector2d OutgoingDirection,
        double TangentLength,
        double Deflection,
        int Sign);
}
