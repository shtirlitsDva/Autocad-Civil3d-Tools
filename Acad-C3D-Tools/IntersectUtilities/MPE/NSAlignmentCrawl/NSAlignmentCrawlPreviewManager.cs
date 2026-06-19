using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// Transient (non-committed) preview of the candidate crawl path. Mirrors the lifecycle of
/// <c>PipePlanDEPreviewManager</c>: a single polyline is shown via the TransientManager and
/// replaced/erased on each refresh. Takes ownership of the polyline passed to <see cref="Show"/>.
/// </summary>
internal sealed class NSAlignmentCrawlPreviewManager : IDisposable
{
    private const short PreviewColorIndex = 1; // red, matching the intended crawl line
    private readonly IntegerCollection _viewports = [];
    private Polyline? _current;

    public void Dispose() => Clear();

    public void Clear()
    {
        if (_current is null)
        {
            return;
        }

        try
        {
            TransientManager.CurrentTransientManager.EraseTransient(_current, _viewports);
        }
        catch
        {
            // Best-effort cleanup of the transient preview entity.
        }

        _current.Dispose();
        _current = null;
    }

    public void Show(Polyline polyline)
    {
        Clear();
        polyline.Color = Color.FromColorIndex(ColorMethod.ByAci, PreviewColorIndex);
        polyline.LineWeight = LineWeight.LineWeight050;
        _current = polyline;
        TransientManager.CurrentTransientManager.AddTransient(
            polyline, TransientDrawingMode.DirectShortTerm, 128, _viewports);
    }
}
