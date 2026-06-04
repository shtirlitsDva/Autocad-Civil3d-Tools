using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace IntersectUtilities.MPE.Ler3DNetwork
{
    // One downhill slope arrowhead: plan position (Cx,Cy) at elevation Cz, pointing
    // along the unit plan direction (Ux,Uy). Size-independent — the manager scales
    // each arrowhead to the current view at draw time.
    internal readonly record struct LerSlopeAnchor(double Cx, double Cy, double Cz, double Ux, double Uy);

    // Draws slope arrowheads as transient 2D polylines and keeps them at a CONSTANT
    // SCREEN size by resizing on Application.Idle whenever the view height changes —
    // the PipePlan marker pattern (PipePlanSharpCornerMarkerManager). Each arrowhead
    // is a 3-vertex "V"; on a view change its vertices are recomputed in place
    // (SetPointAt) and the transient is UpdateTransient'd, so it tracks live zoom.
    internal sealed class LerSlopeArrowManager : IDisposable
    {
        private const double ViewFraction = 1.0 / 120.0;    // arrowhead length / view height
        private const double MinSize = 0.2, MaxSize = 5.0;  // model-unit clamp
        private const double WidthFactor = 0.5;             // barb half-width / length
        private const double ViewChangeTolerance = 1e-6;

        private readonly IntegerCollection _viewportNumbers = new();
        private readonly List<(LerSlopeAnchor Anchor, AcadPolyline Marker)> _markers = new();
        private readonly CadColor _color;

        private Document? _document;
        private double _lastViewHeight = double.NaN;
        private bool _idleHooked;

        // Defaults to magenta (the ground-truth slope overlay); pass a colour to match
        // a different context, e.g. cyan for the inspection lift preview.
        public LerSlopeArrowManager() : this(CadColor.FromRgb(255, 0, 255)) { }

        public LerSlopeArrowManager(CadColor color) => _color = color;

        public void Dispose() => Clear();

        public void Show(Document document, IReadOnlyList<LerSlopeAnchor> anchors)
        {
            Clear();
            if (anchors.Count == 0) return;

            _document = document;
            double size = ComputeSize(document);

            foreach (LerSlopeAnchor a in anchors)
            {
                AcadPolyline pl = new();
                pl.AddVertexAt(0, Point2d.Origin, 0.0, 0.0, 0.0);
                pl.AddVertexAt(1, Point2d.Origin, 0.0, 0.0, 0.0);
                pl.AddVertexAt(2, Point2d.Origin, 0.0, 0.0, 0.0);
                pl.Elevation = a.Cz;
                pl.Normal = Vector3d.ZAxis;
                pl.Color = _color;
                ShapeArrow(pl, a, size);
                _markers.Add((a, pl));
            }

            foreach (var (_, marker) in _markers)
            {
                TransientManager.CurrentTransientManager.AddTransient(
                    marker, TransientDrawingMode.DirectShortTerm, 129, _viewportNumbers);
            }

            _lastViewHeight = TryReadViewHeight(document, out double h) ? h : double.NaN;
            Application.Idle += OnIdle;
            _idleHooked = true;
            document.Editor.UpdateScreen();
        }

        public void Clear()
        {
            if (_idleHooked)
            {
                Application.Idle -= OnIdle;
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

        // Resize to constant screen size whenever the view height changes (pan/zoom).
        private void OnIdle(object? sender, EventArgs e)
        {
            if (_document is null || _markers.Count == 0) return;
            if (!TryReadViewHeight(_document, out double height)) return;
            if (!double.IsNaN(_lastViewHeight) && Math.Abs(height - _lastViewHeight) < ViewChangeTolerance) return;

            _lastViewHeight = height;
            double size = Math.Clamp(height * ViewFraction, MinSize, MaxSize);

            foreach (var (anchor, marker) in _markers)
            {
                ShapeArrow(marker, anchor, size);
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

        // Position the 3 vertices of one arrowhead for the given world size.
        private static void ShapeArrow(AcadPolyline pl, LerSlopeAnchor a, double size)
        {
            double half = size * 0.5, w = size * WidthFactor;
            double px = -a.Uy, py = a.Ux;                  // left perpendicular
            double tipX = a.Cx + a.Ux * half, tipY = a.Cy + a.Uy * half;
            double backX = a.Cx - a.Ux * half, backY = a.Cy - a.Uy * half;
            pl.SetPointAt(0, new Point2d(backX + px * w, backY + py * w));
            pl.SetPointAt(1, new Point2d(tipX, tipY));
            pl.SetPointAt(2, new Point2d(backX - px * w, backY - py * w));
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
}
