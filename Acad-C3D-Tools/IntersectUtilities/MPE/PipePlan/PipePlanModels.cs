using System.Globalization;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed record PipePlanActiveContext(
    PipeSystemEnum System,
    PipeTypeEnum Type,
    int Dn,
    double Width,
    double Radius,
    string LayerName);

internal sealed class PipePlanStoredData
{
    public PipePlanStoredData(
        PipeSystemEnum system,
        PipeTypeEnum type,
        int dn,
        double radius,
        string straightSnapToleranceText,
        IReadOnlyList<Point3d> controlPoints,
        string? objectToken = null)
    {
        ObjectToken = string.IsNullOrWhiteSpace(objectToken) ? Guid.NewGuid().ToString("N") : objectToken;
        System = system;
        Type = type;
        Dn = dn;
        Radius = radius;
        StraightSnapToleranceText = straightSnapToleranceText;
        ControlPoints = [.. controlPoints];
    }

    public string ObjectToken { get; set; }

    public PipeSystemEnum System { get; set; }

    public PipeTypeEnum Type { get; set; }

    public int Dn { get; set; }

    public double Radius { get; set; }

    public string StraightSnapToleranceText { get; set; }

    public List<Point3d> ControlPoints { get; }

    public string SizeDisplay => $"{System} {Type} DN{Dn}";

    public string RadiusDisplay => Radius.ToString("0.###", CultureInfo.InvariantCulture);
}

internal readonly record struct PolylineVertexData(Point2d Point, double Bulge);

internal readonly record struct PipePlanRadiusAnnotation(
    Point3d Center,
    Point3d ArcMidPoint,
    double Radius);

internal readonly record struct PipePlanFilletEndpointMarker(
    Point3d TangentIn,
    Point3d TangentOut);

internal sealed record PipePlanCandidateResult(
    Point3d RawPoint,
    Point3d FinalPoint,
    PipePlanAnalysis Analysis,
    bool StraightSnapActive);

internal sealed record PipePlanEditHandle(
    PipePlanEditHandleKind Kind,
    int Index,
    Point3d GripPoint);

internal sealed record PipePlanEditCandidate(
    IReadOnlyList<Point3d> ControlPoints,
    PipePlanAnalysis Analysis);

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
