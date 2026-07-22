using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CadColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities.MPE.TerrainKoteCompare
{
    internal sealed class TerrainKoteCompareTransientRenderer
    {
        private readonly List<AcEntity> _currentEntities = new List<AcEntity>();

        // The transient manager is bound to the document active when it is read. Because this
        // renderer lives on a session-wide singleton palette, it must be re-acquired for the current
        // document each time we draw, and the same instance reused for the matching erase — never
        // cached at construction. See LERCompareTerrainTransientRenderer for the same rationale.
        private TransientManager? _activeManager;

        public void Show(
            IReadOnlyList<TerrainKoteCompareResultPoint> points,
            double markerSize,
            double textHeight,
            TerrainKoteCompareValueMode valueMode)
        {
            Clear();

            if (points.Count == 0)
            {
                return;
            }

            TransientManager transientManager = TransientManager.CurrentTransientManager;
            _activeManager = transientManager;

            double radius = markerSize > 0.0 ? markerSize : 0.5;
            double height = textHeight > 0.0 ? textHeight : 0.5;

            foreach (TerrainKoteCompareResultPoint point in points)
            {
                CadColor color = TerrainKoteCompareColors.GetCadColor(point.Classification);

                Circle marker = new Circle(point.Position, Vector3d.ZAxis, radius)
                {
                    Color = color
                };
                AddTransient(transientManager, marker);

                // One two-line MText (number over value) — the same single object Create Labels
                // bakes, so the preview and the drawing match exactly apart from this marker circle.
                MText label = new MText
                {
                    Location = TerrainKoteCompareTextLayout.LabelPosition(point.Position, radius),
                    TextHeight = height,
                    Attachment = AttachmentPoint.BottomLeft,
                    Contents = point.FormatLabelContents(valueMode),
                    Color = color
                };
                AddTransient(transientManager, label);
            }
        }

        private void AddTransient(TransientManager transientManager, AcEntity entity)
        {
            _currentEntities.Add(entity);
            // Main (not DirectShortTerm) so the markers are re-projected on every view regen —
            // DirectShortTerm freezes as a 2D snapshot when orbiting in 3D.
            transientManager.AddTransient(
                entity,
                TransientDrawingMode.Main,
                128,
                new IntegerCollection());
        }

        public void Clear()
        {
            // Erase against the manager the transients were added to, not the currently active
            // document's manager.
            TransientManager transientManager = _activeManager ?? TransientManager.CurrentTransientManager;

            foreach (AcEntity entity in _currentEntities)
            {
                try
                {
                    transientManager.EraseTransient(entity, new IntegerCollection());
                }
                catch
                {
                    // Intentionally ignored during transient cleanup.
                }

                entity.Dispose();
            }

            _currentEntities.Clear();
            _activeManager = null;
        }
    }
}
