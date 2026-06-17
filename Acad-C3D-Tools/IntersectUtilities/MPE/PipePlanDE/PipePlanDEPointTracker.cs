using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Live PDDRAW preview driven by the editor's PointMonitor (the same mechanism
/// PPDRAW uses). Each cursor tick applies the Ctrl-held snap and redraws the
/// centreline-plus-bands preview through the moving candidate point.
/// </summary>
internal sealed class PipePlanDEPointTracker : IDisposable
{
    private readonly Document _document;
    private readonly PipePlanDEPreviewManager _preview;
    private readonly IReadOnlyList<Point3d> _points;
    private readonly PipePlanDEParameters _parameters;
    private readonly Func<bool> _flip;

    public PipePlanDEPointTracker(
        Document document,
        PipePlanDEPreviewManager preview,
        IReadOnlyList<Point3d> points,
        PipePlanDEParameters parameters,
        Func<bool> flip)
    {
        _document = document;
        _preview = preview;
        _points = points;
        _parameters = parameters;
        _flip = flip;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose()
    {
        _document.Editor.PointMonitor -= OnPointMonitor;
    }

    private void OnPointMonitor(object? sender, PointMonitorEventArgs eventArgs)
    {
        if (_points.Count == 0)
        {
            return;
        }

        Point3d raw = eventArgs.Context.ComputedPoint;
        Editor editor = _document.Editor;
        double tolerance = PipePlanDESnap.GetSnapTolerance(editor);
        (Point3d candidate, PipePlanDESnapMode snapMode) = PipePlanDESnap.Resolve(raw, _points, PipePlanDESnap.IsCtrlHeld(), tolerance);

        List<Point3d> previewPoints = new(_points.Count + 1);
        previewPoints.AddRange(_points);
        previewPoints.Add(candidate);
        _preview.Show(previewPoints, _parameters, _flip(), snapMode, PipePlanDESnap.GetIndicatorSize(editor));
    }
}
