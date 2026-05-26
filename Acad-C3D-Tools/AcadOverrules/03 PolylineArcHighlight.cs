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

        // Overall scale knob for the warning sign and the angle label.
        private const double SymbolScale = 0.25;

        // Half-width of the warning triangle before SymbolScale, in drawing units.
        // Scales with the polyline width but never drops below this floor.
        private const double WarningMinHalfWidth = 1.0;

        // Text height of the angle readout.
        private const double LabelHeight = 1.0 * SymbolScale;

        // The warning glyph is drawn in orange. The triangle body is left
        // unfilled (transparent) so it does not obscure the geometry underneath.
        private static readonly EntityColor WarningColor = new EntityColor((byte)255, (byte)128, (byte)0);

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
                double signSize = Math.Max(width * 4.0, WarningMinHalfWidth) * SymbolScale;

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
                        DrawAngleLabel(wd, vertPos, incoming, outgoing);
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
        /// Orange warning triangle (unfilled / transparent body) with a solid
        /// orange exclamation mark, anchored at <paramref name="pos"/>.
        /// <paramref name="size"/> is the triangle's base half-width.
        /// </summary>
        private static void DrawWarningSign(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd, Point3d pos, double size)
        {
            // Upward-pointing triangle (offsets around the anchor point).
            // Outline only - the fill is fully transparent (alpha 0).
            Point3dCollection triangle = new Point3dCollection
            {
                new Point3d(0.0,         1.4 * size, 0.0),
                new Point3d(-1.0 * size, -0.6 * size, 0.0),
                new Point3d( 1.0 * size, -0.6 * size, 0.0),
            };
            DrawFilledPolygon(wd, pos, triangle, WarningColor, WarningColor, fillAlpha: 0);

            // Exclamation mark: a vertical bar above a dot, drawn solid.
            double bw = 0.13 * size;

            Point3dCollection bar = new Point3dCollection
            {
                new Point3d(-bw, 0.0,        0.0),
                new Point3d( bw, 0.0,        0.0),
                new Point3d( bw, 0.8 * size, 0.0),
                new Point3d(-bw, 0.8 * size, 0.0),
            };
            DrawFilledPolygon(wd, pos, bar, WarningColor, WarningColor, fillAlpha: 255);

            Point3dCollection dot = new Point3dCollection
            {
                new Point3d(-bw, -0.45 * size, 0.0),
                new Point3d( bw, -0.45 * size, 0.0),
                new Point3d( bw, -0.20 * size, 0.0),
                new Point3d(-bw, -0.20 * size, 0.0),
            };
            DrawFilledPolygon(wd, pos, dot, WarningColor, WarningColor, fillAlpha: 255);
        }

        /// <summary>
        /// Draws a single polygon whose points are offsets around
        /// <paramref name="position"/>. <paramref name="fillAlpha"/> is the fill
        /// opacity (0 = transparent / outline only, 255 = opaque).
        /// </summary>
        private static void DrawFilledPolygon(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd,
            Point3d position,
            Point3dCollection points,
            EntityColor fill,
            EntityColor outline,
            byte fillAlpha)
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
                new TransparencyCollection(1) { new Transparency(fillAlpha) };

            wd.Geometry.PolyPolygon(
                numPolygonPositions, polygonPositions, numPolygonPoints,
                points, outlineColors, outlineTypes, fillColors, fillOpacities);
        }

        /// <summary>
        /// Text showing the corner's deviation from straight (180°) — the smaller
        /// value, e.g. a 178°/182° corner reads "2.0000°". The text is centred on
        /// the outward bisector and aligned to it, pushed out far enough that it
        /// never touches or crosses the polyline.
        /// </summary>
        private static void DrawAngleLabel(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd,
            Point3d vertPos, Vector3d incoming, Vector3d outgoing)
        {
            double deviationDeg = incoming.GetAngleTo(outgoing).ToDeg();
            string label = $"{deviationDeg.ToString("0.0000")}°";

            // Outward bisector: the direction in which the corner opens widest.
            // The rays from the vertex are -incoming and outgoing; their inner
            // bisector negated is (incoming - outgoing), which points outward.
            Vector3d bisector = incoming - outgoing;
            if (bisector.Length < 1e-9)
                bisector = incoming.GetPerpendicularVector();
            bisector = bisector.GetNormal();

            // Read along the bisector line; pick the left-to-right orientation.
            Vector3d dir = bisector;
            if (dir.X < 0.0 || (dir.X == 0.0 && dir.Y < 0.0)) dir = -dir;
            Vector3d up = Vector3d.ZAxis.CrossProduct(dir).GetNormal();

            var extents = AngleTextStyle.ExtentsBox(label, true, false, null);
            double cx = (extents.MinPoint.X + extents.MaxPoint.X) / 2.0;
            double cy = (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0;
            double halfWidth = (extents.MaxPoint.X - extents.MinPoint.X) / 2.0;

            // The text reads along the bisector, so its width reaches back toward
            // the vertex. Place the centre out by halfWidth + one text height so the
            // near edge keeps a full text-height gap from the line.
            Point3d center = vertPos + bisector * (halfWidth + LabelHeight);
            Point3d position = center - dir * cx - up * cy;

            wd.SubEntityTraits.Color = AngleLabelColor;
            wd.Geometry.Text(position, Vector3d.ZAxis, dir, label, true, AngleTextStyle);
        }
    }
}
