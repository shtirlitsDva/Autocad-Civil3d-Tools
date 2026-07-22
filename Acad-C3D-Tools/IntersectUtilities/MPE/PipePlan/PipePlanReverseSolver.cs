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
                // Recover the arc's virtual corner from its OWN tangent lines: the tangent
                // at the arc start and at the arc end, extended until they meet. For a
                // classic line-arc-line fillet this is identical to intersecting the two
                // flanking straights, but it also works when the arc abuts another arc (a
                // PipePlan crowded-corner chain) or sits at the polyline start/end — cases
                // the old "arc must be bracketed by straights" rule rejected outright.
                CircularArc2d arc = polyline.GetArcSegment2dAt(i);
                Point3d arcStart = GetPoint3dAt(polyline, i);
                Point3d arcEnd = GetPoint3dAt(polyline, i + 1);

                Vector3d startTangent = polyline.GetFirstDerivative((double)i + 1e-6);
                Vector3d endTangent = polyline.GetFirstDerivative((double)(i + 1) - 1e-6);
                Vector2d startDir = new(startTangent.X, startTangent.Y);
                Vector2d endDir = new(endTangent.X, endTangent.Y);
                if (startDir.Length <= DistanceTolerance || endDir.Length <= DistanceTolerance)
                {
                    error = "Buens tangent kunne ikke bestemmes.";
                    return false;
                }

                if (!PipePlanGeometryUtil.TryIntersectLines2D(arcStart, startDir, arcEnd, endDir, out Point3d corner))
                {
                    // Tangents are (anti)parallel — a straight or a full half-turn, neither
                    // a fillet corner. Refuse rather than emit a bogus control point.
                    error = "Buens geometri er inkonsistent — kan ikke konvertere.";
                    return false;
                }

                controlPoints.Add(corner);
                // arc.Radius carries ~1 ULP of transcendental noise (R = chord/(2·sin(δ/2)));
                // round to 3 decimals (sub-mm at metre scale) so the UI shows 38 instead of
                // 38.0000000003875.
                radii.Add(Math.Round(arc.Radius, 3));

                i += 1;
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
