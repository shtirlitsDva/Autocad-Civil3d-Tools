using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities
{
    /// <summary>
    /// Draws transient orange markers + "Bi" labels at the problematic bends of a polyline,
    /// used by the CHECKALLPOLYLINERADII inspector.
    /// Geometry is sized as a fraction of the current view height so it reads at a roughly
    /// constant on-screen size. Transient entities are world-sized, so <see cref="Show"/> must
    /// be called again after every zoom to rebuild the geometry for the new view scale.
    /// </summary>
    internal sealed class BendRadiusLabelMarkerManager : IDisposable
    {
        private readonly Autodesk.AutoCAD.Geometry.IntegerCollection _viewportNumbers = [];
        private readonly List<Entity> _markers = [];

        public void Dispose() => Clear();

        /// <summary>Rebuilds and shows the markers, sized to the document's current view.</summary>
        public void Show(Document document, IReadOnlyList<(string label, Point3d position)> bends)
        {
            Clear();
            if (bends.Count == 0) return;

            using ViewTableRecord view = document.Editor.GetCurrentView();
            double textHeight = view.Height / 40.0;
            double dotRadius = view.Height / 150.0;
            AcColor orange = AcColor.FromRgb(255, 128, 0);

            foreach (var (label, position) in bends)
            {
                Circle dot = new(position, Vector3d.ZAxis, dotRadius) { Color = orange };
                _markers.Add(dot);

                DBText text = new()
                {
                    TextString = label,
                    Height = textHeight,
                    Position = new Point3d(position.X + dotRadius * 1.5, position.Y + dotRadius * 1.5, 0.0),
                    Color = orange,
                    TextStyleId = document.Database.Textstyle
                };
                _markers.Add(text);
            }

            foreach (Entity marker in _markers)
            {
                TransientManager.CurrentTransientManager.AddTransient(
                    marker,
                    TransientDrawingMode.DirectShortTerm,
                    129,
                    _viewportNumbers);
            }
        }

        public void Clear()
        {
            if (_markers.Count == 0) return;

            foreach (Entity marker in _markers)
            {
                try
                {
                    TransientManager.CurrentTransientManager.EraseTransient(marker, _viewportNumbers);
                }
                catch
                {
                    // Best-effort cleanup for transient marker entities.
                }

                marker.Dispose();
            }

            _markers.Clear();
        }
    }
}
