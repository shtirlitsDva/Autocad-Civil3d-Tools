using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Windows.Forms;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Ctrl-held magnetic snapping for the PDDRAW point loop. Mirrors PPDRAW's straight
/// snap (project the cursor onto the straight continuation of the previous segment)
/// and additionally snaps to the line perpendicular to the previous segment, so a
/// clean 90° turn is just as easy to hit. The same transform is applied both in the
/// live preview (PointMonitor) and at commit, so what you see is what you place.
/// </summary>
internal enum PipePlanDESnapMode
{
    None,
    Straight,
    Perpendicular,
}

internal static class PipePlanDESnap
{
    private const double DistanceTolerance = 1e-6;

    public static bool IsCtrlHeld()
        => (Control.ModifierKeys & Keys.Control) == Keys.Control;

    /// <summary>View-relative tolerance (~1.25% of the visible height) so the snap feels
    /// the same at any zoom level.</summary>
    public static double GetSnapTolerance(Editor editor)
    {
        try
        {
            using ViewTableRecord view = editor.GetCurrentView();
            return Math.Max(view.Height / 80.0, 0.01);
        }
        catch
        {
            return 0.5;
        }
    }

    /// <summary>Size of the on-screen snap indicator glyphs, view-relative.</summary>
    public static double GetIndicatorSize(Editor editor)
    {
        try
        {
            using ViewTableRecord view = editor.GetCurrentView();
            return Math.Clamp(view.Height / 110.0, 0.05, 3.0);
        }
        catch
        {
            return 0.3;
        }
    }

    public static (Point3d Point, PipePlanDESnapMode Mode) Resolve(Point3d raw, IReadOnlyList<Point3d> points, bool ctrlHeld, double tolerance)
    {
        if (!ctrlHeld || points.Count < 2)
        {
            return (raw, PipePlanDESnapMode.None);
        }

        Point3d previous = points[^2];
        Point3d anchor = points[^1];
        Vector2d segment = new(anchor.X - previous.X, anchor.Y - previous.Y);
        double length = segment.Length;
        if (length <= DistanceTolerance)
        {
            return (raw, PipePlanDESnapMode.None);
        }

        Vector2d along = segment / length;
        Vector2d perpendicular = new(-along.Y, along.X);
        Vector2d offset = new(raw.X - anchor.X, raw.Y - anchor.Y);
        double alongComponent = offset.DotProduct(along);
        double perpComponent = offset.DotProduct(perpendicular);

        // Straight continuation: only forward; its distance from the cursor is the
        // perpendicular component. Perpendicular line: either side; its distance from
        // the cursor is the along component. Snap to whichever guide line is nearer
        // and within tolerance, else leave the point free.
        bool straightValid = alongComponent > DistanceTolerance;
        double straightDistance = Math.Abs(perpComponent);
        double perpendicularDistance = Math.Abs(alongComponent);

        bool useStraight = straightValid && straightDistance <= perpendicularDistance;
        if (useStraight)
        {
            if (straightDistance > tolerance)
            {
                return (raw, PipePlanDESnapMode.None);
            }

            Point3d straightPoint = new(anchor.X + (along.X * alongComponent), anchor.Y + (along.Y * alongComponent), raw.Z);
            return (straightPoint, PipePlanDESnapMode.Straight);
        }

        if (perpendicularDistance > tolerance)
        {
            return (raw, PipePlanDESnapMode.None);
        }

        Point3d perpPoint = new(anchor.X + (perpendicular.X * perpComponent), anchor.Y + (perpendicular.Y * perpComponent), raw.Z);
        return (perpPoint, PipePlanDESnapMode.Perpendicular);
    }
}
