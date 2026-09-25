using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.MPE.MapConnections;

/// <summary>Moves the view onto an LP: a straight jump that keeps the zoom scale, or a zoom to the whole view.</summary>
internal static class LpViewJumper
{
    private const double ZoomPaddingFraction = 0.05;

    /// <summary>Recentres the current view on the station (at the view's mid height), keeping width and height.</summary>
    public static void JumpTo(Editor editor, LpProfileView view, double station)
    {
        Point2d target = view.PointAt(station);
        using ViewTableRecord current = editor.GetCurrentView();
        Matrix3d worldToEye =
            Matrix3d.WorldToPlane(current.ViewDirection)
            * Matrix3d.Displacement(Point3d.Origin - current.Target)
            * Matrix3d.Rotation(current.ViewTwist, current.ViewDirection, current.Target);
        Point3d eye = new Point3d(target.X, target.Y, 0.0).TransformBy(worldToEye);
        current.CenterPoint = new Point2d(eye.X, eye.Y);
        editor.SetCurrentView(current);
    }

    public static void ZoomToView(Editor editor, LpProfileView view)
    {
        Extents3d ext = view.Extents;
        double pad = Math.Max(ext.MaxPoint.X - ext.MinPoint.X, ext.MaxPoint.Y - ext.MinPoint.Y) * ZoomPaddingFraction;
        editor.Zoom(new Extents3d(
            new Point3d(ext.MinPoint.X - pad, ext.MinPoint.Y - pad, 0.0),
            new Point3d(ext.MaxPoint.X + pad, ext.MaxPoint.Y + pad, 0.0)));
    }
}
