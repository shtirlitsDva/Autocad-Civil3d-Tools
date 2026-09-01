using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.MatchBBR
{
    // Draws the comparison result as transient graphics: circles around the BBR blocks, coloured
    // by outcome.
    //
    // Nothing is written to the database. The previous version created real polylines on
    // district-named layers, which meant every look at a result left permanent geometry behind
    // that then had to be found and erased again. A preview is discarded by Clear, by closing the
    // palette, or by ending the session — there is nothing to clean up.
    //
    // TransientManager.CurrentTransientManager is resolved on every call and never cached: it is
    // bound to the active document, so a cached one drawn from a palette that outlives a drawing
    // switch quietly stops working.
    internal sealed class MatchBbrPreview : IDisposable
    {
        private readonly IntegerCollection _viewports = new IntegerCollection();
        private readonly List<Entity> _entities = new List<Entity>();

        public bool IsVisible => _entities.Count > 0;

        public int Show(IEnumerable<ComparisonRow> rows)
        {
            Clear();

            foreach (ComparisonRow row in rows)
            {
                // A row that exists only in Excel has no block, so there is nowhere to draw it.
                if (row.BbrRecord is null)
                {
                    continue;
                }

                Circle circle = new Circle(
                    row.BbrRecord.Position,
                    Vector3d.ZAxis,
                    MatchBbrConstants.MarkerRadius)
                {
                    Color = ColorFor(row.Status),
                };

                _entities.Add(circle);
                TransientManager.CurrentTransientManager.AddTransient(
                    circle,
                    TransientDrawingMode.DirectShortTerm,
                    128,
                    _viewports);
            }

            return _entities.Count;
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
                    // A transient belonging to a drawing that has since closed cannot be erased.
                    // Log it and carry on disposing — the graphics are gone with the document.
                    prdDbg(ex);
                }

                entity.Dispose();
            }

            _entities.Clear();
        }

        public void Dispose() => Clear();

        // ACI colours, chosen to read against both a dark and a light model space and to line up
        // with the row tints in the grid.
        private static CadColor ColorFor(MatchStatus status) =>
            status switch
            {
                MatchStatus.Match => CadColor.FromColorIndex(ColorMethod.ByAci, 3),
                MatchStatus.Afvigelse => CadColor.FromColorIndex(ColorMethod.ByAci, 2),
                MatchStatus.KunITegning => CadColor.FromColorIndex(ColorMethod.ByAci, 1),
                MatchStatus.Dublet => CadColor.FromColorIndex(ColorMethod.ByAci, 6),
                _ => CadColor.FromColorIndex(ColorMethod.ByAci, 7),
            };
    }
}
