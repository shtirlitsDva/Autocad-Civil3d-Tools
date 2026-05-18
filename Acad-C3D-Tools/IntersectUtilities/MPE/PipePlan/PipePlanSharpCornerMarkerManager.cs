using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanSharpCornerMarkerManager : IDisposable
{
    private readonly Autodesk.AutoCAD.Geometry.IntegerCollection _viewportNumbers = [];
    private readonly List<Entity> _markers = [];

    public void Dispose()
    {
        Clear();
    }

    public void Show(Document document, IReadOnlyList<Point3d> sharpCornerPositions, double radius)
    {
        Clear();
        if (sharpCornerPositions.Count == 0 || radius <= 0.0)
        {
            return;
        }

        Autodesk.AutoCAD.Colors.Color circleColor = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 80, 80);

        foreach (Point3d point in sharpCornerPositions)
        {
            Circle circle = new(point, Vector3d.ZAxis, radius)
            {
                Color = circleColor
            };
            _markers.Add(circle);
        }

        foreach (Entity marker in _markers)
        {
            TransientManager.CurrentTransientManager.AddTransient(
                marker,
                TransientDrawingMode.DirectShortTerm,
                129,
                _viewportNumbers);
        }

        document.Editor.UpdateScreen();
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
}
