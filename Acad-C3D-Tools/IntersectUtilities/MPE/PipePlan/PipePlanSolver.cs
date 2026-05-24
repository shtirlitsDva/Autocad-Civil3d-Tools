using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanSolver
{
    private const double DistanceTolerance = 1e-6;
    private const double AngleTolerance = 1e-6;

    public PipePlanAnalysis Analyze(IReadOnlyList<Point3d> points, double radius)
    {
        double[] radii = new double[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            radii[i] = (i == 0 || i == points.Count - 1) ? 0.0 : radius;
        }

        return Analyze(points, radii);
    }

    public PipePlanAnalysis Analyze(IReadOnlyList<Point3d> points, IReadOnlyList<double> radii)
    {
        if (points.Count < 2)
        {
            return PipePlanAnalysis.Raw(points, true, "Vælg mindst to punkter.");
        }

        if (radii.Count != points.Count)
        {
            return PipePlanAnalysis.Invalid(points, "Intern fejl: data inkonsistent. Kør PPCONVERT.");
        }

        PipePlanBendGeometry?[] bends = new PipePlanBendGeometry?[points.Count];

        for (int i = 1; i < points.Count - 1; i++)
        {
            double radius = radii[i];
            if (radius <= DistanceTolerance)
            {
                return PipePlanAnalysis.Invalid(points, $"Bukkeradius ved hjørne {i + 1} skal være > 0.");
            }

            PipePlanBendStatus status = PipePlanBendCalculator.TryCompute(points[i - 1], points[i], points[i + 1], radius, out PipePlanBendGeometry bend);
            switch (status)
            {
                case PipePlanBendStatus.Bend:
                    bends[i] = bend;
                    break;
                case PipePlanBendStatus.Straight:
                    continue;
                case PipePlanBendStatus.Degenerate:
                    return PipePlanAnalysis.Invalid(points, $"To punkter ligger oven på hinanden ved hjørne {i + 1}.");
                case PipePlanBendStatus.Reversal:
                    return PipePlanAnalysis.Invalid(points, "180° vending kan ikke bukkes.");
                case PipePlanBendStatus.Infeasible:
                    return PipePlanAnalysis.Invalid(points, "Hjørnet er for skarpt for denne radius.");
            }
        }

        for (int segmentIndex = 0; segmentIndex < points.Count - 1; segmentIndex++)
        {
            double length = points[segmentIndex].DistanceTo(points[segmentIndex + 1]);
            double trimStart = bends[segmentIndex]?.TangentLength ?? 0.0;
            double trimEnd = bends[segmentIndex + 1]?.TangentLength ?? 0.0;

            if ((trimStart + trimEnd) - length > DistanceTolerance)
            {
                string message = $"Segment {segmentIndex + 1} for kort til radius.";
                return PipePlanAnalysis.Invalid(points, message);
            }
        }

        List<PolylineVertexData> vertexData = [new PolylineVertexData(To2D(points[0]), 0.0)];
        List<PipePlanRadiusAnnotation> radiusAnnotations = [];
        List<PipePlanFilletEndpointMarker> filletEndpointMarkers = [];
        for (int i = 1; i < points.Count - 1; i++)
        {
            if (bends[i] is not PipePlanBendGeometry bend)
            {
                continue;
            }

            Point2d tangentIn2D = new(bend.TangentIn.X, bend.TangentIn.Y);
            Point2d tangentOut2D = new(bend.TangentOut.X, bend.TangentOut.Y);
            double bulge = bend.Sign * Math.Tan(bend.Deflection / 4.0);

            AppendVertex(vertexData, tangentIn2D, bulge);
            AppendVertex(vertexData, tangentOut2D, 0.0);
            radiusAnnotations.Add(CreateRadiusAnnotation(tangentIn2D, bend));
            filletEndpointMarkers.Add(new PipePlanFilletEndpointMarker(bend.TangentIn, bend.TangentOut));
        }

        AppendVertex(vertexData, To2D(points[^1]), 0.0);

        return PipePlanAnalysis.Curved(points, vertexData, radiusAnnotations, filletEndpointMarkers, "Tegning OK.");
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

    private static PipePlanRadiusAnnotation CreateRadiusAnnotation(Point2d tangentIn, PipePlanBendGeometry bend)
    {
        Vector2d offsetToCenter = RotateLeft(bend.IncomingDirection) * (bend.Sign * bend.Radius);
        Point2d center = tangentIn + offsetToCenter;

        Vector2d startRadius = tangentIn - center;
        Vector2d midRadius = Rotate(startRadius, bend.Sign * bend.Deflection / 2.0);
        Point2d arcMidPoint = center + midRadius;

        return new PipePlanRadiusAnnotation(
            new Point3d(center.X, center.Y, bend.Vertex.Z),
            new Point3d(arcMidPoint.X, arcMidPoint.Y, bend.Vertex.Z),
            bend.Radius);
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

}
