using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipeSizeOption
{
    public PipeSizeOption(string name)
    {
        Name = name;
        RadiusText = "1.0";
    }

    public string Name { get; }

    public string RadiusText { get; set; }

    public bool TryGetRadius(out double radius)
    {
        return PipePlanParsing.TryParsePositiveDouble(RadiusText, out radius);
    }

    public string GetLayerName()
    {
        return $"PIPEPLAN_{Name.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)}";
    }

    public double GetGlobalWidth()
    {
        return TryGetGlobalWidth(Name, out double globalWidth) ? globalWidth : 0.0;
    }

    public static bool TryGetGlobalWidth(string? sizeName, out double globalWidth)
    {
        globalWidth = 0.0;
        if (string.IsNullOrWhiteSpace(sizeName))
        {
            return false;
        }

        string digits = new(sizeName.Where(char.IsDigit).ToArray());
        if (!PipePlanParsing.TryParseDouble(digits, out double nominalDiameter) || nominalDiameter <= 0.0)
        {
            return false;
        }

        globalWidth = (nominalDiameter * 2.0) / 1000.0;
        return true;
    }

    public override string ToString()
    {
        return Name;
    }
}

internal sealed class PipePlanStoredData
{
    public PipePlanStoredData(string sizeName, string radiusText, string straightSnapToleranceText, IReadOnlyList<Point3d> controlPoints, string? objectToken = null)
    {
        ObjectToken = string.IsNullOrWhiteSpace(objectToken) ? Guid.NewGuid().ToString("N") : objectToken;
        SizeName = sizeName;
        RadiusText = radiusText;
        StraightSnapToleranceText = straightSnapToleranceText;
        ControlPoints = [.. controlPoints];
    }

    public string ObjectToken { get; set; }

    public string SizeName { get; set; }

    public string RadiusText { get; set; }

    public string StraightSnapToleranceText { get; set; }

    public List<Point3d> ControlPoints { get; }
}

internal readonly record struct PolylineVertexData(Point2d Point, double Bulge);

internal readonly record struct PipePlanRadiusAnnotation(
    Point3d Center,
    Point3d ArcMidPoint,
    double Radius);

internal readonly record struct PipePlanFilletEndpointMarker(
    Point3d TangentIn,
    Point3d TangentOut);

internal sealed record PipePlanFittingProposal(
    PipePlanFittingKind Kind,
    Point3d IntersectionPoint,
    Vector3d StaticDirection,
    Vector3d BranchDirection);

internal sealed record PipePlanCandidateResult(
    Point3d RawPoint,
    Point3d FinalPoint,
    PipePlanAnalysis Analysis,
    bool StraightSnapActive,
    PipePlanFittingProposal? FittingProposal = null);

internal sealed record PipePlanEditHandle(
    PipePlanEditHandleKind Kind,
    int Index,
    Point3d GripPoint);

internal sealed record PipePlanEditCandidate(
    IReadOnlyList<Point3d> ControlPoints,
    PipePlanAnalysis Analysis,
    PipePlanFittingProposal? FittingProposal = null);

internal enum PipePlanStatusKind
{
    Info,
    Ok,
    Snap,
    Warning,
    Error
}

internal enum PipePlanPreviewKind
{
    Standard,
    StraightSnap
}

internal enum PipePlanEditHandleKind
{
    Vertex,
    Segment
}

internal enum PipePlanFittingKind
{
    Tee,
    Cross
}
