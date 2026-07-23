using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using IntersectUtilities.MPE.PipePlan;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Transient grips + radius labels for a PDEDIT session, patterned on
/// <c>PipePlanHandleMarkerManager</c>: an orange circle at each routing control point
/// (Vertex handle), a yellow square at each control-segment midpoint (Segment handle),
/// and an "R=<min> (akse <centre>)" label at each fillet. Drawn at rest over the real
/// baked polylines (which stay visible); candidate drags use the preview manager instead.
/// </summary>
internal sealed class PipePlanDEHandleMarkerManager : IDisposable
{
    private static readonly Color VertexColor = Color.FromRgb(255, 170, 0);
    private static readonly Color SegmentColor = Color.FromRgb(255, 210, 60);
    private static readonly Color LabelColor = Color.FromRgb(0, 100, 45);

    private readonly IntegerCollection _viewports = [];
    private readonly List<Entity> _markers = [];

    public void Dispose() => Clear();

    public void Show(
        Document document,
        IReadOnlyList<Point3d> controlPoints,
        IReadOnlyList<PipePlanRadiusAnnotation> radiusAnnotations,
        double half)
    {
        Clear();
        if (controlPoints.Count < 2)
        {
            return;
        }

        double markerSize = GetMarkerSize(document);

        foreach (Point3d point in controlPoints)
        {
            _markers.Add(new Circle(point, Vector3d.ZAxis, markerSize * 0.35) { Color = VertexColor });
        }

        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Point3d mid = new(
                (controlPoints[i].X + controlPoints[i + 1].X) / 2.0,
                (controlPoints[i].Y + controlPoints[i + 1].Y) / 2.0,
                (controlPoints[i].Z + controlPoints[i + 1].Z) / 2.0);

            Polyline square = new();
            double h = markerSize * 0.25;
            square.AddVertexAt(0, new Point2d(mid.X - h, mid.Y - h), 0.0, 0.0, 0.0);
            square.AddVertexAt(1, new Point2d(mid.X + h, mid.Y - h), 0.0, 0.0, 0.0);
            square.AddVertexAt(2, new Point2d(mid.X + h, mid.Y + h), 0.0, 0.0, 0.0);
            square.AddVertexAt(3, new Point2d(mid.X - h, mid.Y + h), 0.0, 0.0, 0.0);
            square.Closed = true;
            square.Color = SegmentColor;
            _markers.Add(square);
        }

        double textHeight = GetTextHeight(document);
        foreach (PipePlanRadiusAnnotation annotation in radiusAnnotations)
        {
            _markers.Add(CreateRadiusLabel(annotation, half, textHeight));
        }

        foreach (Entity marker in _markers)
        {
            TransientManager.CurrentTransientManager.AddTransient(marker, TransientDrawingMode.DirectShortTerm, 129, _viewports);
        }
    }

    private static MText CreateRadiusLabel(PipePlanRadiusAnnotation annotation, double half, double textHeight)
    {
        double rMin = annotation.Radius - half;
        MText label = new();
        label.SetDatabaseDefaults();
        label.Color = LabelColor;
        label.Contents = $"R={rMin.ToString("0.###", CultureInfo.CurrentCulture)}";
        label.TextHeight = textHeight;
        label.Attachment = AttachmentPoint.MiddleCenter;

        Vector3d direction = annotation.ArcMidPoint - annotation.Center;
        label.Location = direction.Length <= 1e-6
            ? annotation.ArcMidPoint
            : annotation.ArcMidPoint + (direction.GetNormal() * Math.Max(textHeight * 0.8, annotation.Radius * 0.05));
        return label;
    }

    public void Clear()
    {
        foreach (Entity marker in _markers)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(marker, _viewports);
            }
            catch
            {
                // Best effort cleanup for transient marker entities.
            }

            marker.Dispose();
        }

        _markers.Clear();
    }

    public double GetPickTolerance(Document document) => GetMarkerSize(document) * 0.9;

    private static double GetMarkerSize(Document document)
    {
        try
        {
            using ViewTableRecord view = document.Editor.GetCurrentView();
            return Math.Clamp(view.Height / 90.0, 0.2, 4.0);
        }
        catch
        {
            return 0.5;
        }
    }

    private static double GetTextHeight(Document document)
    {
        try
        {
            using ViewTableRecord view = document.Editor.GetCurrentView();
            return Math.Clamp(view.Height / 40.0, 0.3, 8.0);
        }
        catch
        {
            return 1.0;
        }
    }
}
