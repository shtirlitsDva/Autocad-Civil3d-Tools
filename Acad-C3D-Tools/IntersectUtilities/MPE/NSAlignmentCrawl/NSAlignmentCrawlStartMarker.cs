using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

using CadColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// A transient "X" drawn at the alignment start (the snapped point on the xref pipe/block, not the
/// raw click) so the user can see where station 0 will be — they were otherwise guessing. Two crossing
/// lines kept at a CONSTANT SCREEN size by resizing on Application.Idle whenever the view height
/// changes, the same pattern as <see cref="Ler3DNetwork.LerSlopeArrowManager"/>.
/// </summary>
internal sealed class NSAlignmentCrawlStartMarker : IDisposable
{
    private const double ViewFraction = 1.0 / 50.0;   // marker size / view height
    private const double MinSize = 0.3, MaxSize = 8.0; // model-unit clamp
    private const double ViewChangeTolerance = 1e-6;

    private readonly IntegerCollection _viewports = new();
    private readonly CadColor _color;

    private Document? _document;
    private Point2d _center;
    private Line? _a;
    private Line? _b;
    private double _size = 1.0;
    private double _lastViewHeight = double.NaN;
    private bool _idleHooked;

    public NSAlignmentCrawlStartMarker(CadColor color) => _color = color;

    public void Dispose() => Clear();

    public void Show(Document document, Point2d center)
    {
        // Already shown in this document → cheap reposition, so cursor-following ticks don't churn
        // through erase/re-add of the transients.
        if (_a is not null && _b is not null && ReferenceEquals(_document, document))
        {
            MoveTo(center);
            return;
        }

        Clear();
        _document = document;
        _center = center;
        _size = ComputeSize(document);

        _a = new Line { Color = _color };
        _b = new Line { Color = _color };
        Shape(_size);

        TransientManager tm = TransientManager.CurrentTransientManager;
        tm.AddTransient(_a, TransientDrawingMode.DirectShortTerm, 130, _viewports);
        tm.AddTransient(_b, TransientDrawingMode.DirectShortTerm, 130, _viewports);

        _lastViewHeight = TryReadViewHeight(document, out double h) ? h : double.NaN;
        Application.Idle += OnIdle;
        _idleHooked = true;
        document.Editor.UpdateScreen();
    }

    /// <summary>Repositions an already-shown marker (cursor following) without rebuilding transients.</summary>
    private void MoveTo(Point2d center)
    {
        if (_a is null || _b is null)
        {
            return;
        }

        _center = center;
        Shape(_size);

        TransientManager tm = TransientManager.CurrentTransientManager;
        try
        {
            tm.UpdateTransient(_a, _viewports);
            tm.UpdateTransient(_b, _viewports);
        }
        catch
        {
            // Best-effort transient refresh — ignore failures during rapid cursor movement.
        }
    }

    public void Clear()
    {
        if (_idleHooked)
        {
            Application.Idle -= OnIdle;
            _idleHooked = false;
        }

        TransientManager tm = TransientManager.CurrentTransientManager;
        foreach (Line? line in new[] { _a, _b })
        {
            if (line is null)
            {
                continue;
            }

            try
            {
                tm.EraseTransient(line, _viewports);
            }
            catch
            {
                // Best-effort cleanup of the transient marker entity.
            }

            line.Dispose();
        }

        _a = null;
        _b = null;
        _document = null;
    }

    private void OnIdle(object? sender, EventArgs e)
    {
        if (_document is null || _a is null || _b is null)
        {
            return;
        }

        if (!TryReadViewHeight(_document, out double height))
        {
            return;
        }

        if (!double.IsNaN(_lastViewHeight) && Math.Abs(height - _lastViewHeight) < ViewChangeTolerance)
        {
            return;
        }

        _lastViewHeight = height;
        _size = Math.Clamp(height * ViewFraction, MinSize, MaxSize);
        Shape(_size);

        TransientManager tm = TransientManager.CurrentTransientManager;
        try
        {
            tm.UpdateTransient(_a, _viewports);
            tm.UpdateTransient(_b, _viewports);
        }
        catch
        {
            // Best-effort transient refresh — ignore failures during pan/zoom races.
        }
    }

    private void Shape(double size)
    {
        double h = size * 0.5;
        _a!.StartPoint = new Point3d(_center.X - h, _center.Y - h, 0.0);
        _a!.EndPoint = new Point3d(_center.X + h, _center.Y + h, 0.0);
        _b!.StartPoint = new Point3d(_center.X - h, _center.Y + h, 0.0);
        _b!.EndPoint = new Point3d(_center.X + h, _center.Y - h, 0.0);
    }

    private static double ComputeSize(Document document)
    {
        return TryReadViewHeight(document, out double h)
            ? Math.Clamp(h * ViewFraction, MinSize, MaxSize)
            : 1.0;
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
