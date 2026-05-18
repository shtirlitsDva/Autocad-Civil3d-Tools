using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanSharpCornerMarkerManager : IDisposable
{
    private const double ViewFraction = 1.0 / 50.0;
    private const double ViewSizeChangeTolerance = 1e-6;

    private readonly Autodesk.AutoCAD.Geometry.IntegerCollection _viewportNumbers = [];
    private readonly List<(Point3d Center, Circle Marker)> _markers = [];

    private Document? _document;
    private double _lastViewHeight = double.NaN;
    private bool _idleHooked;

    public void Dispose()
    {
        Clear();
    }

    public void Show(Document document, IReadOnlyList<Point3d> sharpCornerPositions)
    {
        Clear();
        if (sharpCornerPositions.Count == 0)
        {
            return;
        }

        _document = document;
        Autodesk.AutoCAD.Colors.Color circleColor = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 80, 80);
        double worldRadius = ComputeWorldRadius(document);

        foreach (Point3d point in sharpCornerPositions)
        {
            Circle circle = new(point, Vector3d.ZAxis, worldRadius)
            {
                Color = circleColor
            };
            _markers.Add((point, circle));
        }

        foreach (var (_, marker) in _markers)
        {
            TransientManager.CurrentTransientManager.AddTransient(
                marker,
                TransientDrawingMode.DirectShortTerm,
                129,
                _viewportNumbers);
        }

        _lastViewHeight = TryReadViewHeight(document, out double height) ? height : double.NaN;
        Autodesk.AutoCAD.ApplicationServices.Application.Idle += OnIdle;
        _idleHooked = true;

        document.Editor.UpdateScreen();
    }

    public void Clear()
    {
        if (_idleHooked)
        {
            Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdle;
            _idleHooked = false;
        }

        if (_markers.Count == 0)
        {
            _document = null;
            return;
        }

        foreach (var (_, marker) in _markers)
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
        _document = null;
    }

    private void OnIdle(object? sender, EventArgs e)
    {
        if (_document is null || _markers.Count == 0)
        {
            return;
        }

        if (!TryReadViewHeight(_document, out double currentHeight))
        {
            return;
        }

        if (!double.IsNaN(_lastViewHeight) && Math.Abs(currentHeight - _lastViewHeight) < ViewSizeChangeTolerance)
        {
            return;
        }

        _lastViewHeight = currentHeight;
        double worldRadius = currentHeight * ViewFraction;

        foreach (var (_, marker) in _markers)
        {
            marker.Radius = worldRadius;
            try
            {
                TransientManager.CurrentTransientManager.UpdateTransient(marker, _viewportNumbers);
            }
            catch
            {
                // Best effort transient refresh — ignore failures during pan/zoom races.
            }
        }
    }

    private static double ComputeWorldRadius(Document document)
    {
        return TryReadViewHeight(document, out double height) ? height * ViewFraction : 1.0;
    }

    private static bool TryReadViewHeight(Document document, out double height)
    {
        height = 0.0;
        try
        {
            using ViewTableRecord view = document.Editor.GetCurrentView();
            height = view.Height;
            return height > 0.0;
        }
        catch
        {
            return false;
        }
    }
}
