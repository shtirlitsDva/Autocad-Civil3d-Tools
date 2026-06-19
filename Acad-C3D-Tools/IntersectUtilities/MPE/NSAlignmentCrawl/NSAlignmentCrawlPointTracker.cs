using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// Live preview driver: on every cursor tick during the end-point prompt it resolves the shortest
/// crawl path from the fixed start to the cursor (snapped to the nearest pipe) and refreshes the
/// transient preview. Uses the editor's PointMonitor, the same mechanism PDDRAW/PPDRAW use.
/// </summary>
internal sealed class NSAlignmentCrawlPointTracker : IDisposable
{
    private readonly Document _document;
    private readonly NSAlignmentCrawlPreviewManager _preview;
    private readonly CrawlSession _session;

    public NSAlignmentCrawlPointTracker(Document document, NSAlignmentCrawlPreviewManager preview, CrawlSession session)
    {
        _document = document;
        _preview = preview;
        _session = session;
        _document.Editor.PointMonitor += OnPointMonitor;
    }

    public void Dispose() => _document.Editor.PointMonitor -= OnPointMonitor;

    private void OnPointMonitor(object? sender, PointMonitorEventArgs eventArgs)
    {
        try
        {
            Point3d raw = eventArgs.Context.ComputedPoint;
            if (_session.TryBuildPath(raw, out List<(Point2d Pt, double OutBulge)> vertices))
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
