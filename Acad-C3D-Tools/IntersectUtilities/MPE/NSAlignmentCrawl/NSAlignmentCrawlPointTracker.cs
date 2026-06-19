using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// Live preview driver: on every cursor tick during a waypoint prompt it asks the supplied builder
/// for the candidate path (already-committed segments plus the shortest crawl from the current source
/// to the cursor) and refreshes the transient preview. Uses the editor's PointMonitor, the same
/// mechanism PDDRAW/PPDRAW use. The builder is a delegate so the command can keep mutating the
/// committed prefix and current source as waypoints are added.
/// </summary>
internal sealed class NSAlignmentCrawlPointTracker : IDisposable
{
    private readonly Document _document;
    private readonly NSAlignmentCrawlPreviewManager _preview;
    private readonly Func<Point3d, List<(Point2d Pt, double OutBulge)>?> _build;

    public NSAlignmentCrawlPointTracker(
        Document document,
        NSAlignmentCrawlPreviewManager preview,
        Func<Point3d, List<(Point2d Pt, double OutBulge)>?> build)
    {
        _document = document;
        _preview = preview;
        _build = build;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose() => _document.Editor.PointMonitor -= OnPointMonitor;

    private void OnPointMonitor(object? sender, PointMonitorEventArgs eventArgs)
    {
        try
        {
            Point3d raw = eventArgs.Context.ComputedPoint;
            List<(Point2d Pt, double OutBulge)>? vertices = _build(raw);
            if (vertices is not null)
            {
                Polyline? polyline = NSAlignmentCrawlPolylineBuilder.Build(vertices, NSAlignmentCrawlConstants.OutputLayer);
                if (polyline is not null)
                {
                    _preview.Show(polyline);
                    return;
                }
            }

            _preview.Clear();
        }
        catch
        {
            // A PointMonitor handler must never throw — a bad tick simply shows no preview.
        }
    }
}
