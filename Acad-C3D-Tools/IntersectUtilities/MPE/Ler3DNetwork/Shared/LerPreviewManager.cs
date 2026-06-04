using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.Ler3DNetwork
{
    // One item to draw: a polyline path, its colour, and its line weight.
    internal readonly record struct LerPreviewItem(
        IReadOnlyList<Point3d> Points,
        CadColor Color,
        LineWeight Weight);

    // Draws transient (non-persisted) preview geometry, matching the lifecycle
    // used by Ler2Project/PipePlan: short-term direct transients tracked in a
    // list and erased on Clear. Owns nothing in the database. Shared by every
    // Ler3DNetwork command.
    internal sealed class LerPreviewManager : IDisposable
    {
        private readonly IntegerCollection _viewports = new();
        private readonly List<Entity> _entities = new();

        public void Show(IEnumerable<LerPreviewItem> items)
        {
            Clear();
            foreach (LerPreviewItem item in items)
            {
                if (item.Points.Count < 2) continue;

                Polyline3d polyline = new(
                    Poly3dType.SimplePoly,
                    new Point3dCollection(AsArray(item.Points)),
                    false)
                {
                    Color = item.Color,
                    LineWeight = item.Weight
                };

                _entities.Add(polyline);
                TransientManager.CurrentTransientManager.AddTransient(
                    polyline,
                    TransientDrawingMode.DirectShortTerm,
                    128,
                    _viewports);
            }
        }

        public void Clear()
        {
            foreach (Entity entity in _entities)
            {
                try
                {
                    TransientManager.CurrentTransientManager.EraseTransient(entity, _viewports);
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                }
                entity.Dispose();
            }
            _entities.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        private static Point3d[] AsArray(IReadOnlyList<Point3d> points)
        {
            Point3d[] array = new Point3d[points.Count];
            for (int i = 0; i < points.Count; i++) array[i] = points[i];
            return array;
        }
    }
}
