using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace GraphViewV3;

/// <summary>Selects and zooms to a drawing entity by handle. Runs on the AutoCAD main
/// thread (invoked from WPF click handlers, which dispatch on that same thread).</summary>
internal static class AcadSelect
{
    public static void SelectAndZoom(string handle)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null || string.IsNullOrWhiteSpace(handle)) return;

        try
        {
            var h = new Handle(Convert.ToInt64(handle, 16));
            if (!doc.Database.TryGetObjectId(h, out var id) || id.IsNull) return;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var ed = doc.Editor;
                ed.SetImpliedSelection(new[] { id });
                if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                {
                    try { ZoomTo(ed, ent.GeometricExtents); } catch { }
                }
                tr.Commit();
            }
            doc.Editor.UpdateScreen();
        }
        catch { /* selection is best-effort; never throw into the UI */ }
    }

    private static void ZoomTo(Editor ed, Extents3d ext)
    {
        var min = ext.MinPoint;
        var max = ext.MaxPoint;
        const double margin = 1.4;
        using var view = ed.GetCurrentView();
        view.Width = Math.Max((max.X - min.X) * margin, 1.0);
        view.Height = Math.Max((max.Y - min.Y) * margin, 1.0);
        view.CenterPoint = new Point2d((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0);
        ed.SetCurrentView(view);
    }
}
