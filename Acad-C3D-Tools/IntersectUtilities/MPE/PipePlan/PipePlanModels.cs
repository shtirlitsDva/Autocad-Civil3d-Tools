using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed record PipePlanActiveContext(
    PipeSystemEnum System,
    PipeTypeEnum Type,
    int Dn,
    double Radius,
    string LayerName);

internal sealed class PipePlanStoredData
{
    public PipePlanStoredData(
        PipeSystemEnum system,
        PipeTypeEnum type,
        int dn,
        IReadOnlyList<double> bendRadii,
        string straightSnapToleranceText,
        IReadOnlyList<Point3d> controlPoints,
        string? objectToken = null)
    {
        if (bendRadii.Count != controlPoints.Count)
        {
            throw new ArgumentException("bendRadii must be aligned with controlPoints.");
        }

        ObjectToken = string.IsNullOrWhiteSpace(objectToken) ? Guid.NewGuid().ToString("N") : objectToken;
        System = system;
        Type = type;
        Dn = dn;
        BendRadii = [.. bendRadii];
        StraightSnapToleranceText = straightSnapToleranceText;
        ControlPoints = [.. controlPoints];
    }

    public string ObjectToken { get; set; }

    public PipeSystemEnum System { get; set; }

    public PipeTypeEnum Type { get; set; }

    public int Dn { get; set; }

    public List<double> BendRadii { get; }

    public string StraightSnapToleranceText { get; set; }

    public List<Point3d> ControlPoints { get; }

    public string SizeDisplay => $"{System} {Type} DN{Dn}";

    public string RadiusDisplay
    {
        get
        {
            var interior = BendRadii.Skip(1).Take(Math.Max(0, BendRadii.Count - 2)).Where(r => r > 0.0).ToList();
            if (interior.Count == 0) return "-";
            double first = interior[0];
            bool uniform = interior.All(r => Math.Abs(r - first) < 1e-6);
            return uniform
                ? first.ToString("0.###", CultureInfo.InvariantCulture)
                : $"{interior.Min().ToString("0.###", CultureInfo.InvariantCulture)}–{interior.Max().ToString("0.###", CultureInfo.InvariantCulture)}";
        }
    }

    public static List<double> CreateUniformRadii(int controlPointCount, double radius)
    {
        List<double> radii = new(controlPointCount);
        for (int i = 0; i < controlPointCount; i++)
        {
            radii.Add(i == 0 || i == controlPointCount - 1 ? 0.0 : radius);
        }
        return radii;
    }
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
    bool StraightSnapActive,
    Point3d? TangentCornerPoint = null,
    int TangentDropCount = 0);

internal sealed record PipePlanEditHandle(
    PipePlanEditHandleKind Kind,
    int Index,
    Point3d GripPoint);

internal sealed record PipePlanEditCandidate(
    PipePlanEditDraft Draft,
    PipePlanAnalysis Analysis);

internal sealed record PipePlanEditDraft(
    IReadOnlyList<Point3d> ControlPoints,
    IReadOnlyList<double> BendRadii);

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
    StraightSnap,
    Tangent
}

// Snapshot of the PP2 endpoint the cursor is hovering near while tangent mode is active.
// SourceId identifies the polyline the snap came from; revalidation at commit time
// reopens this id to verify the entity still exists, still carries PipePlan metadata,
// and still has an endpoint at Pp2Anchor — without it the sticky cache can latch
// onto stale or unrelated geometry near the same anchor.
// Direction is unit-length and always points INTO PP2 from Pp2Anchor (the trackers flip
// the polyline's first-derivative when the snap point is PP2's end endpoint).
// Pp2Length is needed to bound Case-2 in the fillet solver: when PP1's bend tangent overshoots
// PP2's near endpoint by more than the remaining PP2 length, the fillet is rejected.
internal readonly record struct PipePlanTangentSnap(
    ObjectId SourceId,
    Point3d Pp2Anchor,
    Vector2d Direction,
    double Pp2Length);

internal enum PipePlanEditHandleKind
{
    Vertex,
    Segment
}
