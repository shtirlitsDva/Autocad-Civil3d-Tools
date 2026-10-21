#if BRICSCAD
using Teigha.DatabaseServices;
using Teigha.Geometry;
#else
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
#endif

using System;
using System.Collections.Generic;

namespace AcadOverrules.VertexCircles
{
    /// <summary>
    /// The corner of one arc segment: where the tangents at the arc's start and end meet.
    /// </summary>
    internal readonly struct ArcCorner
    {
        public ArcCorner(Point3d arcStart, Point3d corner, Point3d arcEnd)
        {
            ArcStart = arcStart;
            Corner = corner;
            ArcEnd = arcEnd;
        }

        public Point3d ArcStart { get; }
        public Point3d Corner { get; }
        public Point3d ArcEnd { get; }
    }

    /// <summary>
    /// Finds the tangent intersection of every arc segment of a polyline. Shared by the draw
    /// overrule and the object snap overrule, so the circle on screen and the snap point
    /// are the same point.
    /// </summary>
    internal static class TangentIntersections
    {
        /// <summary>
        /// Arcs closer to a half circle than this are skipped: the corner runs off towards
        /// infinity as the arc approaches 180 degrees. 0.98 is an arc of about 177 degrees,
        /// whose corner lies some 40 radii away - already of no use on a drawing.
        /// </summary>
        private const double MaxAbsBulge = 0.98;

        /// <summary>Chords shorter than this are degenerate and have no direction.</summary>
        private const double MinChordLength = 1e-9;

        /// <summary>The corners of every arc segment that has one, in segment order.</summary>
        public static List<ArcCorner> Find(Polyline pline)
        {
            var corners = new List<ArcCorner>();

            int vertexCount = pline.NumberOfVertices;
            int segmentCount = pline.Closed ? vertexCount : vertexCount - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                if (pline.GetSegmentType(i) != SegmentType.Arc) continue;

                Point3d start = pline.GetPoint3dAt(i);
                Point3d end = pline.GetPoint3dAt((i + 1) % vertexCount);

                if (TryCorner(start, end, pline.GetBulgeAt(i), pline.Normal, out Point3d corner))
                    corners.Add(new ArcCorner(start, corner, end));
            }

            return corners;
        }

        /// <summary>
        /// The corner of the arc from <paramref name="start"/> to <paramref name="end"/> with
        /// <paramref name="bulge"/>, measured counterclockwise about <paramref name="normal"/>.
        ///
        /// The corner lies on the perpendicular bisector of the chord, on the side the arc
        /// bulges to. With chord length c and included angle D, its distance from the chord
        /// midpoint is (c/2)·tan(D/2). The bulge is tan(D/4), so tan(D/2) = 2b/(1-b²) and the
        /// distance is c·b/(1-b²) - signed, because a positive bulge (counterclockwise arc)
        /// bulges to the right of the chord.
        /// </summary>
        private static bool TryCorner(
            Point3d start, Point3d end, double bulge, Vector3d normal, out Point3d corner)
        {
            corner = start;

            if (Math.Abs(bulge) > MaxAbsBulge) return false;

            Vector3d chord = end - start;
            double length = chord.Length;
            if (length < MinChordLength) return false;

            Vector3d right = chord.CrossProduct(normal).GetNormal();
            Point3d middle = start + chord * 0.5;

            corner = middle + right * (length * bulge / (1.0 - bulge * bulge));
            return true;
        }
    }
}
