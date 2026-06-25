using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using ColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;

namespace NorsynDistrictZones.Reactors;

/// <summary>
/// Draws an invalid user polyline in transient red (no DB entity created) so the
/// user sees why their line was rejected, then clears on the next valid action or
/// regen. Mirrors the TransientManager pattern used elsewhere in the codebase.
/// </summary>
internal sealed class InvalidPolylineMarker : IDisposable
{
    private readonly List<Entity> _markers = new();
    private readonly IntegerCollection _viewports = new();

    /// <summary>Show a red ghost copy of the rejected polyline's geometry.</summary>
    public void Show(AcPolyline source)
    {
        Clear();
        var clone = (AcPolyline)source.Clone();
        clone.Color = AcColor.FromColorIndex(ColorMethod.ByAci, 1); // red
        clone.ConstantWidth = 0;
        _markers.Add(clone);
        TransientManager.CurrentTransientManager.AddTransient(
            clone, TransientDrawingMode.DirectShortTerm, 128, _viewports);
    }

    public void Clear()
    {
        foreach (Entity m in _markers)
        {
            try { TransientManager.CurrentTransientManager.EraseTransient(m, _viewports); }
            catch { /* best effort */ }
            m.Dispose();
        }
        _markers.Clear();
    }

    public void Dispose() => Clear();
}
