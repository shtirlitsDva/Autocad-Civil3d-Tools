using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using System;

using IntersectUtilities.UtilsCommon;

namespace AcadOverrules
{
    /// <summary>
    /// Overrule that highlights arc segments of polylines by drawing them
    /// with a cyan color overlay. Straight segments are not affected.
    ///
    /// In addition it analyses the tangency between consecutive segments:
    /// - A junction where at least one segment is an arc and the incoming and
    ///   outgoing tangents differ by more than <see cref="TangencyToleranceRad"/>
    ///   radians is flagged with an orange warning sign (a kink where the arc
    ///   does not run tangent into its neighbour).
    /// - A line-line junction that is not collinear is labelled with the
    ///   deviation angle in degrees (4 decimals).
    /// </summary>
    public class PolylineArcHighlight : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
        // Cyan color (ACI index 4)
        private const short CyanColor = 4;

        // White/black color (ACI 7) for the angle readout text.
        private const short AngleLabelColor = 7;

        // Two tangents are considered "tangent"/"collinear" when the angle
        // between them is at or below this value (radians).
        private const double TangencyToleranceRad = 1e-6;

        // Half-width of the warning triangle, in drawing units. Scales with the
        // polyline width but never drops below this floor so it stays visible.
        private const double WarningMinHalfWidth = 1.0;

        // Text height of the angle readout.
        private const double LabelHeight = 1.0;

        private static readonly EntityColor WarningFill = new EntityColor((byte)255, (byte)128, (byte)0);
        private static readonly EntityColor WarningOutline = new EntityColor((byte)0, (byte)0, (byte)0);
        private static readonly EntityColor MarkColor = new EntityColor((byte)0, (byte)0, (byte)0);

        private static readonly Autodesk.AutoCAD.GraphicsInterface.TextStyle AngleTextStyle =
            new Autodesk.AutoCAD.GraphicsInterface.TextStyle(
                "Arial", "Arial", LabelHeight, 0.0, 0.0, 0.0,
                false, false, false, false, false, false, "MyStd");

        public PolylineArcHighlight()
        {
            base.SetCustomFilter();
        }

        public override bool IsApplicable(RXObject overruledSubject)
        {
            if (overruledSubject == null) return false;
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;
                if (pline.NumberOfVertices < 2) return false;
                if (pline.Length < 0.1) return false;
                // Only apply to polylines that have arc segments
                if (!pline.HasBulges) return false;
                return true;
            }
            return false;
        }

        public override bool WorldDraw(
            Autodesk.AutoCAD.GraphicsInterface.Drawable drawable,
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd)
        {
            // First, draw the original polyline
            base.WorldDraw(drawable, wd);

            Polyline pline = (Polyline)drawable;

            // Set color to cyan for arc segments
            wd.SubEntityTraits.Color = CyanColor;

            // Iterate through all segments
            int segmentCount = pline.NumberOfVertices - 1;
            if (pline.Closed) segmentCount = pline.NumberOfVertices;

            for (int i = 0; i < segmentCount; i++)
            {
                // Check if this segment is an arc (bulge != 0)
                double bulge = pline.GetBulgeAt(i);
                if (bulge == 0) continue;

                // Draw the arc segment on top with cyan color
                // Using Polyline method which respects the polyline's width
                wd.Geometry.Polyline(pline, i, 1);
            }

            DrawTangencyWarnings(pline, wd);

            return true;
        }

        /// <summary>
        /// Walks every junction between two consecutive segments and either marks
        /// a non-tangent arc junction with a warning sign or labels a non-collinear
        /// line-line junction with its deviation angle.
        /// </summary>
        private static void DrawTangencyWarnings(
            Polyline pline, Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd)
        {
            try
            {
                int nVerts = pline.NumberOfVertices;
                int segCount = pline.Closed ? nVerts : nVerts - 1;
                int junctionCount = pline.Closed ? segCount : segCount - 1;
                if (junctionCount <= 0) return;

                double width = pline.ConstantWidthSafe();
                if (width <= 0.0) width = 0.25;
                double signSize = Math.Max(width * 4.0, WarningMinHalfWidth);

                for (int j = 0; j < junctionCount; j++)
                {
                    int segA = j;
                    int segB = (j + 1) % segCount;

                    var tanA = SegmentTangents(pline, segA);
                    var tanB = SegmentTangents(pline, segB);
                    if (tanA == null || tanB == null) continue;

                    Vector3d incoming = tanA.Value.end;
                    Vector3d outgoing = tanB.Value.start;
                    if (incoming.Length < 1e-9 || outgoing.Length < 1e-9) continue;

                    double deviationRad = incoming.GetAngleTo(outgoing);
                    if (deviationRad <= TangencyToleranceRad) continue; // tangent / collinear -> OK

                    Point3d vertPos = pline.GetPoint3dAt((j + 1) % nVerts);

                    bool arcInvolved =
                        pline.GetSegmentType(segA) == SegmentType.Arc ||
                        pline.GetSegmentType(segB) == SegmentType.Arc;

                    if (arcInvolved)
                        DrawWarningSign(wd, vertPos, signSize);
                    else
                        DrawAngleLabel(wd, vertPos, deviationRad.ToDeg(), incoming);
                }
            }
            catch
            {
                // WorldDraw runs on every redraw; never let a geometry edge-case
                // throw out of the draw callback and destabilise rendering.
            }
        }

        /// <summary>
        /// Forward (travel-direction) unit tangents at the start and end of a
        /// polyline segment. Returns null for segments that are neither a line
        /// nor an arc (coincident / empty / point) and therefore have no tangent.
        /// </summary>
        private static (Vector3d start, Vector3d end)? SegmentTangents(Polyline pline, int seg)
        {
            SegmentType st = pline.GetSegmentType(seg);

            if (st == SegmentType.Line)
            {
                Vector3d dir = pline.GetLineSegmentAt(seg).Direction.GetNormal();
                return (dir, dir);
            }

            if (st == SegmentType.Arc)
            {
                double bulge = pline.GetBulgeAt(seg);
                CircularArc3d arc = pline.GetArcSegmentAt(seg);
                Point3d center = arc.Center;
                Vector3d normal = pline.Normal;

                Point3d pStart = pline.GetPoint3dAt(seg);
                Point3d pEnd = pline.GetPoint3dAt((seg + 1) % pline.NumberOfVertices);

                return (ArcTangent(pStart, center, normal, bulge),
                        ArcTangent(pEnd, center, normal, bulge));
            }

            return null;
        }

        /// <summary>
        /// Tangent to a circular arc at point <paramref name="p"/>, oriented in the
        /// direction of travel. A positive bulge is a counter-clockwise arc, so the
        /// forward tangent is normal x radial; a negative bulge reverses it.
        /// </summary>
        private static Vector3d ArcTangent(Point3d p, Point3d center, Vector3d normal, double bulge)
        {
            Vector3d radial = (p - center).GetNormal();
            Vector3d tangent = normal.CrossProduct(radial).GetNormal();
            return bulge < 0.0 ? -tangent : tangent;
        }

        /// <summary>
        /// Orange warning triangle with a black exclamation mark, anchored at
        /// <paramref name="pos"/>. <paramref name="size"/> is the triangle's base
        /// half-width.
        /// </summary>
        private static void DrawWarningSign(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd, Point3d pos, double size)
        {
            // Upward-pointing triangle (offsets around the anchor point).
            Point3dCollection triangle = new Point3dCollection
            {
                new Point3d(0.0,         1.4 * size, 0.0),
                new Point3d(-1.0 * size, -0.6 * size, 0.0),
                new Point3d( 1.0 * size, -0.6 * size, 0.0),
            };
            DrawFilledPolygon(wd, pos, triangle, WarningFill, WarningOutline);

            // Exclamation mark: a vertical bar above a dot.
            double bw = 0.13 * size;

            Point3dCollection bar = new Point3dCollection
            {
                new Point3d(-bw, 0.0,        0.0),
                new Point3d( bw, 0.0,        0.0),
                new Point3d( bw, 0.8 * size, 0.0),
                new Point3d(-bw, 0.8 * size, 0.0),
            };
            DrawFilledPolygon(wd, pos, bar, MarkColor, MarkColor);

            Point3dCollection dot = new Point3dCollection
            {
                new Point3d(-bw, -0.45 * size, 0.0),
                new Point3d( bw, -0.45 * size, 0.0),
                new Point3d( bw, -0.20 * size, 0.0),
                new Point3d(-bw, -0.20 * size, 0.0),
            };
            DrawFilledPolygon(wd, pos, dot, MarkColor, MarkColor);
        }

        /// <summary>
        /// Draws a single filled polygon whose points are offsets around
        /// <paramref name="position"/>.
        /// </summary>
        private static void DrawFilledPolygon(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd,
            Point3d position,
            Point3dCollection points,
            EntityColor fill,
            EntityColor outline)
        {
            UInt32Collection numPolygonPositions = new UInt32Collection(1) { 1 };
            Point3dCollection polygonPositions = new Point3dCollection { position };
            UInt32Collection numPolygonPoints = new UInt32Collection(1) { (uint)points.Count };
            EntityColorCollection outlineColors = new EntityColorCollection(1) { outline };
            Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection outlineTypes =
                new Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection
                {
                    Autodesk.AutoCAD.GraphicsInterface.Linetype.Solid
                };
            EntityColorCollection fillColors = new EntityColorCollection(1) { fill };
            TransparencyCollection fillOpacities =
                new TransparencyCollection(1) { new Transparency((byte)255) };

            wd.Geometry.PolyPolygon(
                numPolygonPositions, polygonPositions, numPolygonPoints,
                points, outlineColors, outlineTypes, fillColors, fillOpacities);
        }

        /// <summary>
        /// Horizontal text showing the junction deviation angle, e.g. "1.2345°",
        /// offset clear of the polyline.
        /// </summary>
        private static void DrawAngleLabel(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd,
            Point3d vertPos, double angleDeg, Vector3d incoming)
        {
            string label = $"{angleDeg.ToString("0.0000")}°";

            Vector3d perp = incoming.GetPerpendicularVector().GetNormal();
            var extents = AngleTextStyle.ExtentsBox(label, true, false, null);

            Point3d basePos =
                vertPos
                + perp * (LabelHeight * 1.5)
                - Vector3d.XAxis * (extents.MaxPoint.X / 2.0);

            wd.SubEntityTraits.Color = AngleLabelColor;
            wd.Geometry.Text(basePos, Vector3d.ZAxis, Vector3d.XAxis, label, true, AngleTextStyle);
        }
    }
}
