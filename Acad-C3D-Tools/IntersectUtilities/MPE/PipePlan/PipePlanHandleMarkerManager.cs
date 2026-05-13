using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace PipePlan.Plugin;

internal sealed class PipePlanHandleMarkerManager : IDisposable
{
    private readonly Autodesk.AutoCAD.Geometry.IntegerCollection _viewportNumbers = [];
    private readonly List<Entity> _markers = [];

    public void Dispose()
    {
        Clear();
    }

    public void Show(Document document, IReadOnlyList<Point3d> controlPoints)
    {
        Clear();
        if (controlPoints.Count < 2)
        {
            return;
        }

        double markerSize = GetMarkerSize(document);
        Autodesk.AutoCAD.Colors.Color vertexColor = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 170, 0);
        Autodesk.AutoCAD.Colors.Color segmentColor = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 210, 60);

        foreach (Point3d point in controlPoints)
        {
            Circle circle = new(point, Vector3d.ZAxis, markerSize * 0.35)
            {
                Color = vertexColor
            };
            _markers.Add(circle);
        }

        for (int index = 0; index < controlPoints.Count - 1; index++)
        {
            Point3d midpoint = new(
                (controlPoints[index].X + controlPoints[index + 1].X) / 2.0,
                (controlPoints[index].Y + controlPoints[index + 1].Y) / 2.0,
                (controlPoints[index].Z + controlPoints[index + 1].Z) / 2.0);

            Autodesk.AutoCAD.DatabaseServices.Polyline square = new();
            square.AddVertexAt(0, new Point2d(midpoint.X - markerSize * 0.25, midpoint.Y - markerSize * 0.25), 0.0, 0.0, 0.0);
            square.AddVertexAt(1, new Point2d(midpoint.X + markerSize * 0.25, midpoint.Y - markerSize * 0.25), 0.0, 0.0, 0.0);
            square.AddVertexAt(2, new Point2d(midpoint.X + markerSize * 0.25, midpoint.Y + markerSize * 0.25), 0.0, 0.0, 0.0);
            square.AddVertexAt(3, new Point2d(midpoint.X - markerSize * 0.25, midpoint.Y + markerSize * 0.25), 0.0, 0.0, 0.0);
            square.Closed = true;
            square.Color = segmentColor;
            _markers.Add(square);
        }

        foreach (Entity marker in _markers)
        {
            TransientManager.CurrentTransientManager.AddTransient(
                marker,
                TransientDrawingMode.DirectShortTerm,
                129,
                _viewportNumbers);
        }
    }

    public void Clear()
    {
        if (_markers.Count == 0)
        {
            return;
        }

        foreach (Entity marker in _markers)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(marker, _viewportNumbers);
            }
            catch
            {
                // Best effort cleanup for transient marker entities.
            }

            marker.Dispose();
        }

        _markers.Clear();
    }

    public double GetPickTolerance(Document document)
    {
        return GetMarkerSize(document) * 0.9;
    }

    private static double GetMarkerSize(Document document)
    {
        using ViewTableRecord view = document.Editor.GetCurrentView();
        return Math.Clamp(view.Height / 90.0, 0.2, 4.0);
    }
}
