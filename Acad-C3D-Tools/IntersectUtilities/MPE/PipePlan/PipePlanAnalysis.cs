using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanAnalysis
{
    private PipePlanAnalysis(
        IReadOnlyList<Point3d> controlPoints,
        IReadOnlyList<PolylineVertexData> vertices,
        IReadOnlyList<PipePlanRadiusAnnotation> radiusAnnotations,
        IReadOnlyList<PipePlanFilletEndpointMarker> filletEndpointMarkers,
        bool isFeasible,
        string message,
        PipePlanPreviewKind previewKind)
    {
        ControlPoints = controlPoints;
        Vertices = vertices;
        RadiusAnnotations = radiusAnnotations;
        FilletEndpointMarkers = filletEndpointMarkers;
        IsFeasible = isFeasible;
        Message = message;
        PreviewKind = previewKind;
    }

    public IReadOnlyList<Point3d> ControlPoints { get; }

    public IReadOnlyList<PolylineVertexData> Vertices { get; }

    public IReadOnlyList<PipePlanRadiusAnnotation> RadiusAnnotations { get; }

    public IReadOnlyList<PipePlanFilletEndpointMarker> FilletEndpointMarkers { get; }

    public bool IsFeasible { get; }

    public string Message { get; }

    public PipePlanPreviewKind PreviewKind { get; }

    public static PipePlanAnalysis Raw(IReadOnlyList<Point3d> points, bool isFeasible, string message)
    {
        List<PolylineVertexData> vertices = points
            .Select(point => new PolylineVertexData(new Point2d(point.X, point.Y), 0.0))
            .ToList();
        return new PipePlanAnalysis(points, vertices, [], [], isFeasible, message, PipePlanPreviewKind.Standard);
    }

    public static PipePlanAnalysis Curved(
        IReadOnlyList<Point3d> points,
        IReadOnlyList<PolylineVertexData> vertices,
        IReadOnlyList<PipePlanRadiusAnnotation> radiusAnnotations,
        IReadOnlyList<PipePlanFilletEndpointMarker> filletEndpointMarkers,
        string message)
    {
        return new PipePlanAnalysis(points, vertices, radiusAnnotations, filletEndpointMarkers, true, message, PipePlanPreviewKind.Standard);
    }

    public static PipePlanAnalysis Invalid(IReadOnlyList<Point3d> points, string message)
    {
        return Raw(points, false, message);
    }

    public PipePlanAnalysis WithPreviewKind(PipePlanPreviewKind previewKind)
    {
        return new PipePlanAnalysis(ControlPoints, Vertices, RadiusAnnotations, FilletEndpointMarkers, IsFeasible, Message, previewKind);
    }

    public Polyline CreatePolyline()
    {
        Polyline polyline = new();
        for (int index = 0; index < Vertices.Count; index++)
        {
            PolylineVertexData vertex = Vertices[index];
            polyline.AddVertexAt(index, vertex.Point, vertex.Bulge, 0.0, 0.0);
        }

        return polyline;
    }

    public Autodesk.AutoCAD.Colors.Color GetPreviewColor()
    {
        if (!IsFeasible)
        {
            return Autodesk.AutoCAD.Colors.Color.FromRgb(210, 45, 45);
        }

        return PreviewKind switch
        {
            PipePlanPreviewKind.StraightSnap => Autodesk.AutoCAD.Colors.Color.FromRgb(30, 120, 220),
            PipePlanPreviewKind.Tangent => Autodesk.AutoCAD.Colors.Color.FromRgb(0, 200, 200),
            _ => Autodesk.AutoCAD.Colors.Color.FromRgb(0, 170, 70),
        };
    }
}
