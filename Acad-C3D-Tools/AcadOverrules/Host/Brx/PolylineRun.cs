using System;

using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.GraphicsInterface;

using Polyline = Teigha.DatabaseServices.Polyline;

namespace AcadOverrules
{
    /// <summary>
    /// Draws a contiguous run of a polyline's segments with the traits the
    /// <see cref="WorldDraw"/> holds now, as AutoCAD's <c>Geometry.Polyline(pline, from, count)</c>
    /// does (Host\Acad).
    ///
    /// BricsCAD's managed <c>WorldGeometry.Polyline(Polyline, int, int)</c> is a stub that
    /// returns false and draws nothing (TD_Mgd V26.2). The run is drawn from primitives
    /// instead: each stretch of consecutive straight segments as one point polyline (so a
    /// linetype pattern runs on across its vertices) and each arc segment as a true arc
    /// through its start, middle and end.
    ///
    /// Primitives, never a stand-in entity: a temporary Polyline drawn as a nested drawable
    /// recursed into the overrule and carried its own segment numbering (2026-10-12).
    /// Each segment carries the selection marker the polyline itself gives it, its index
    /// plus one, so a pick or an object snap on the drawn graphics names the polyline's
    /// own segment. Segment widths are not drawn; colour, lineweight and linetype are.
    /// </summary>
    internal static class PolylineRun
    {
        public static void Draw(WorldDraw wd, Polyline pline, int fromIndex, int count)
        {
            int vertexCount = pline.NumberOfVertices;
            if (count <= 0 || vertexCount < 2) return;

            Vector3d normal = pline.Normal;
            var straight = new Point3dCollection();
            int straightFrom = fromIndex;

            for (int k = 0; k < count; k++)
            {
                int segment = fromIndex + k;

                //A run never wraps, but the closing segment of a closed polyline ends at vertex 0.
                Point3d start = pline.GetPoint3dAt(segment % vertexCount);
                Point3d end = pline.GetPoint3dAt((segment + 1) % vertexCount);

                if (pline.GetSegmentType(segment) == SegmentType.Arc)
                {
                    DrawStraight(wd, straight, normal, straightFrom);

                    //A polyline's parameter runs linearly in angle along an arc segment, so
                    //half way through the segment's parameter range is the arc's midpoint.
                    Point3d middle = pline.GetPointAtParameter(segment + 0.5);
                    wd.SubEntityTraits.SetSelectionMarker(Marker(segment));
                    wd.Geometry.CircularArc(start, middle, end, ArcType.ArcSimple);
                    continue;
                }

                if (straight.Count == 0)
                {
                    straight.Add(start);
                    straightFrom = segment;
                }
                straight.Add(end);
            }

            DrawStraight(wd, straight, normal, straightFrom);
        }

        /// <summary>
        /// Draws the pending straight stretch, if any, and empties it. The stretch's first
        /// segment is <paramref name="firstSegment"/>; the call numbers the following
        /// segments on from its marker.
        /// </summary>
        private static void DrawStraight(
            WorldDraw wd, Point3dCollection points, Vector3d normal, int firstSegment)
        {
            if (points.Count >= 2)
            {
                IntPtr marker = Marker(firstSegment);
                wd.SubEntityTraits.SetSelectionMarker(marker);
                wd.Geometry.Polyline(points, normal, marker);
            }

            points.Clear();
        }

        /// <summary>The selection marker of a polyline segment: its index plus one.</summary>
        private static IntPtr Marker(int segment) => new IntPtr(segment + 1);
    }
}
