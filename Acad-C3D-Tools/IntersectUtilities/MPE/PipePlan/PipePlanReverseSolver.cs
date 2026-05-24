using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed record PipePlanReverseSolverResult(
    IReadOnlyList<Point3d> ControlPoints,
    IReadOnlyList<double> BendRadii,
    IReadOnlyList<Point3d> SharpCornerPositions);

internal static class PipePlanReverseSolver
{
    private const double DistanceTolerance = 1e-6;
    private const double AngleTolerance = 1e-4;

    public static bool TryConvert(Polyline polyline, double sharpCornerRadius, out PipePlanReverseSolverResult? result, out string error)
    {
        result = null;
        error = string.Empty;

        int vertexCount = polyline.NumberOfVertices;
        if (vertexCount < 2)
        {
            error = "Polylinjen skal have mindst to hjørner.";
            return false;
        }

        int segmentCount = vertexCount - 1;
        List<Point3d> controlPoints = [];
        List<double> radii = [];
        List<Point3d> sharpCorners = [];

        controlPoints.Add(GetPoint3dAt(polyline, 0));
        radii.Add(0.0);

        int i = 0;
        while (i < segmentCount)
        {
            SegmentType segmentType = polyline.GetSegmentType(i);

            if (segmentType == SegmentType.Arc)
            {
                if (i == 0 || i == segmentCount - 1)
                {
                    error = "Polylinjen skal starte og slutte med et lige segment.";
                    return false;
                }
                if (polyline.GetSegmentType(i - 1) != SegmentType.Line ||
                    polyline.GetSegmentType(i + 1) != SegmentType.Line)
                {
                    error = "Hver bue skal omsluttes af lige segmenter.";
                    return false;
                }

                LineSegment2d prevLine = polyline.GetLineSegment2dAt(i - 1);
                CircularArc2d arc = polyline.GetArcSegment2dAt(i);
                LineSegment2d nextLine = polyline.GetLineSegment2dAt(i + 1);

                Vector2d prevDir = prevLine.EndPoint - prevLine.StartPoint;
                Vector2d nextDir = nextLine.EndPoint - nextLine.StartPoint;

                Point3d prevOrigin = new(prevLine.StartPoint.X, prevLine.StartPoint.Y, 0.0);
                Point3d nextOrigin = new(nextLine.StartPoint.X, nextLine.StartPoint.Y, 0.0);

                if (!PipePlanGeometryUtil.TryIntersectLines2D(prevOrigin, prevDir, nextOrigin, nextDir, out Point3d corner))
                {
                    error = "Buens geometri er inkonsistent — kan ikke konvertere.";
                    return false;
                }

                controlPoints.Add(corner);
                // arc.Radius is computed from the polyline's bulge via trig
                // (R = chord / (2·sin(δ/2))), so the recovered value carries
                // ~1 ULP of floating-point noise per transcendental call. Round
                // to 3 decimal places — sub-millimetre precision when drawings
                // are in metres, well below physical bend-radius tolerance, and
                // enough to keep the UI showing 38 instead of 38.0000000003875.
                radii.Add(Math.Round(arc.Radius, 3));

                i += 2;
                continue;
            }

            if (i + 1 < segmentCount && polyline.GetSegmentType(i + 1) == SegmentType.Line)
            {
                Point3d prev = GetPoint3dAt(polyline, i);
                Point3d here = GetPoint3dAt(polyline, i + 1);
                Point3d next = GetPoint3dAt(polyline, i + 2);

                Vector2d incoming = new(here.X - prev.X, here.Y - prev.Y);
                Vector2d outgoing = new(next.X - here.X, next.Y - here.Y);

                if (incoming.Length <= DistanceTolerance || outgoing.Length <= DistanceTolerance)
                {
                    i += 1;
                    continue;
                }

                Vector2d incomingUnit = incoming / incoming.Length;
                Vector2d outgoingUnit = outgoing / outgoing.Length;
                double dot = Math.Clamp(incomingUnit.DotProduct(outgoingUnit), -1.0, 1.0);
                double deflection = Math.Acos(dot);

                if (deflection > AngleTolerance)
                {
                    controlPoints.Add(here);
                    radii.Add(sharpCornerRadius);
                    sharpCorners.Add(here);
                }
            }

            i += 1;
        }

        controlPoints.Add(GetPoint3dAt(polyline, vertexCount - 1));
        radii.Add(0.0);

        if (controlPoints.Count < 2)
        {
            error = "Polylinjen har færre end to hjørner.";
            return false;
        }

        result = new PipePlanReverseSolverResult(controlPoints, radii, sharpCorners);
        return true;
    }

    private static Point3d GetPoint3dAt(Polyline polyline, int index)
    {
        Point2d p = polyline.GetPoint2dAt(index);
        return new Point3d(p.X, p.Y, polyline.Elevation);
    }
}
