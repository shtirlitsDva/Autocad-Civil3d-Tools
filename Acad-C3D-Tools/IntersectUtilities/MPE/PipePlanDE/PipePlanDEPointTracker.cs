using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Live PDDRAW preview driven by the editor's PointMonitor (the same mechanism
/// PPDRAW uses). Each cursor tick applies the Ctrl-held snap and redraws the
/// centreline-plus-bands preview through the moving candidate point, using the
/// currently active bend radius for the pending corner.
/// </summary>
internal sealed class PipePlanDEPointTracker : IDisposable
{
    private readonly Document _document;
    private readonly PipePlanDEPreviewManager _preview;
    private readonly IReadOnlyList<Point3d> _points;
    private readonly IReadOnlyList<double> _committedRadii;
    private readonly Func<double> _effectiveRadius;
    private readonly PipePlanDEParameters _parameters;
    private readonly Func<bool> _flip;
    private readonly Func<bool> _straight;

    public PipePlanDEPointTracker(
        Document document,
        PipePlanDEPreviewManager preview,
        IReadOnlyList<Point3d> points,
        IReadOnlyList<double> committedRadii,
        Func<double> effectiveRadius,
        PipePlanDEParameters parameters,
        Func<bool> flip,
        Func<bool> straight)
    {
        _document = document;
        _preview = preview;
        _points = points;
        _committedRadii = committedRadii;
        _effectiveRadius = effectiveRadius;
        _parameters = parameters;
        _flip = flip;
        _straight = straight;
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

        double[] previewRadii = BuildPreviewRadii(previewPoints.Count);
        _preview.Show(previewPoints, previewRadii, _parameters, _flip(), _straight(), snapMode, PipePlanDESnap.GetIndicatorSize(editor));
    }

    // The committed radii for existing corners, plus the currently active radius on the
    // last committed point (which becomes an interior corner once the candidate is added).
    private double[] BuildPreviewRadii(int totalCount)
    {
        double[] radii = new double[totalCount];
        for (int i = 0; i < totalCount; i++)
        {
            radii[i] = i < _committedRadii.Count ? _committedRadii[i] : 0.0;
        }

        // Endpoints never bend; the pending corner (second-to-last vertex) uses the
        // active radius so the live preview reflects the chosen value.
        if (totalCount >= 3)
        {
            radii[totalCount - 2] = _effectiveRadius();
        }

        radii[0] = 0.0;
        radii[totalCount - 1] = 0.0;
        return radii;
    }
}
