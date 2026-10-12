using Autodesk.AutoCAD.GraphicsInterface;

using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace AcadOverrules
{
    /// <summary>
    /// Draws a contiguous run of a polyline's segments with the traits the
    /// <see cref="WorldDraw"/> holds now. AutoCAD's own call does exactly that; the BricsCAD
    /// twin (Host\Brx) has to build it, because BricsCAD's call draws nothing.
    /// </summary>
    internal static class PolylineRun
    {
        public static void Draw(WorldDraw wd, Polyline pline, int fromIndex, int count) =>
            wd.Geometry.Polyline(pline, fromIndex, count);
    }
}
