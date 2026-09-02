using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using System;
using System.Linq;

using AcadOverrules.VertexCircles;

namespace AcadOverrules
{
    /// <summary>
    /// Draws a circle at every vertex of every <see cref="Polyline"/> on the layers the user
    /// has selected. Unlike <see cref="DraftPolylineVerticeMark"/> the layer, the size and the
    /// colour are not hard coded - they come from the active profile in
    /// <see cref="VertexCirclesSettingsService"/>, edited with TOGGLEPOLYVERTICESSETTINGS.
    ///
    /// A vertex is classified by the segments touching it and each class gets its own style,
    /// so a bend can be told from a plain corner at a glance - see <see cref="VertexClass"/>.
    ///
    /// The circle is styled relative to the polyline it belongs to:
    /// - Radius is a fixed setting, it does not follow the polyline width.
    /// - Colour is either a fully saturated marker colour on the complementary hue of the
    ///   polyline's own colour, or one fixed ACI, see <see cref="MarkerColor"/>.
    /// - Lineweight is a configurable multiple of the polyline's lineweight.
    /// - Linetype is a fixed setting, Continuous by default. Whether a dashed pattern shows
    ///   on a circle this small depends on LTSCALE - a linetype pattern is measured in
    ///   drawing units, not in fractions of the circumference.
    ///
    /// Segment-level differentiation is a different overrule: <see cref="PolylineArcHighlight"/>
    /// redraws the arc segments themselves and flags non-tangent junctions.
    /// </summary>
    public class PolylineVertexCircles : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
        /// <summary>
        /// What a vertex sits between. Determined by the incoming and outgoing segment, so it
        /// is a property of the vertex's neighbours rather than of the vertex itself.
        /// </summary>
        private enum VertexClass
        {
            /// <summary>Every segment meeting the vertex is a line.</summary>
            Straight = 0,

            /// <summary>At least one segment meeting the vertex is an arc.</summary>
            Arc = 1,
        }

        /// <summary>
        /// Lineweight (hundredths of a mm) substituted when the polyline's effective
        /// lineweight is zero or unresolved (ByLayer/ByBlock/Default) - multiplying those
        /// would be a no-op. 25 is AutoCAD's LWDEFAULT.
        /// </summary>
        private const int FallbackLineWeightHundredthsMm = 25;

        /// <summary>
        /// ACI substituted when the effective colour cannot be resolved to an RGB value.
        /// </summary>
        private const short FallbackColorIndex = 7;

        /// <summary>
        /// Below this HSL saturation a colour is treated as achromatic (white, black, grey).
        /// Such colours have no meaningful hue to take the complement of.
        /// </summary>
        private const float AchromaticSaturationLimit = 0.15f;

        /// <summary>
        /// Marker colour for polylines that are white, black or grey. Vivid magenta reads
        /// clearly against both a dark and a light background and is not a colour these
        /// drawings otherwise use.
        /// </summary>
        private static readonly EntityColor AchromaticMarkerColor =
            new EntityColor((byte)255, (byte)0, (byte)255);

        /// <summary>
        /// The concrete lineweights AutoCAD supports, ascending. The negative enum members
        /// (ByLayer, ByBlock, ByLineWeightDefault) are not real widths and are filtered out.
        /// </summary>
        private static readonly LineWeight[] ConcreteLineWeights =
            Enum.GetValues(typeof(LineWeight)).Cast<LineWeight>()
                .Where(lw => (int)lw >= 0)
                .OrderBy(lw => (int)lw)
                .ToArray();

        private static VertexCirclesSettings Settings =>
            VertexCirclesSettingsService.Instance.Current;

        public PolylineVertexCircles()
        {
            base.SetCustomFilter();
        }

        public override bool IsApplicable(RXObject overruledSubject)
        {
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;
                if (pline.NumberOfVertices < 1) return false;

                return LayerFilterMatcher.Matches(Settings.LayerFilters, pline.Layer);
            }
            return false;
        }

        public override bool WorldDraw(
            Autodesk.AutoCAD.GraphicsInterface.Drawable drawable,
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd)
        {
            //Draw the polyline itself first, then the vertex circles on top
            base.WorldDraw(drawable, wd);

            Polyline pline = (Polyline)drawable;
            VertexCirclesSettings settings = Settings;

            if (pline.NumberOfVertices < 1) return true;

            //Both classes derive colour and lineweight from the polyline, so read its traits
            //once here - the first class to draw overwrites them.
            System.Drawing.Color polylineRgb = EffectiveRgb(pline, wd.SubEntityTraits);
            LineWeight polylineLineWeight = wd.SubEntityTraits.LineWeight;

            VertexClass[] classes = ClassifyVertices(pline, out int straightCount, out int arcCount);

            //One trait assignment per class, not per vertex.
            DrawClass(wd, pline, classes, VertexClass.Straight, straightCount,
                settings.StraightVertex, polylineRgb, polylineLineWeight);

            DrawClass(wd, pline, classes, VertexClass.Arc, arcCount,
                settings.ArcVertex, polylineRgb, polylineLineWeight);

            return true;
        }

        /// <summary>
        /// The class of every vertex, indexed by vertex number.
        ///
        /// Vertex <c>i</c> is bounded by the incoming segment <c>i-1</c> and the outgoing
        /// segment <c>i</c>. On a closed polyline both indices wrap; on an open one the first
        /// vertex has no incoming segment and the last has no outgoing one.
        /// </summary>
        private static VertexClass[] ClassifyVertices(
            Polyline pline, out int straightCount, out int arcCount)
        {
            int vertexCount = pline.NumberOfVertices;
            int segmentCount = pline.Closed ? vertexCount : vertexCount - 1;

            var classes = new VertexClass[vertexCount];
            straightCount = 0;
            arcCount = 0;

            for (int i = 0; i < vertexCount; i++)
            {
                int incoming = pline.Closed && segmentCount > 0
                    ? (i - 1 + segmentCount) % segmentCount
                    : i - 1;

                bool touchesArc =
                    IsArcSegment(pline, incoming, segmentCount) ||
                    IsArcSegment(pline, i, segmentCount);

                classes[i] = touchesArc ? VertexClass.Arc : VertexClass.Straight;

                if (touchesArc) arcCount++;
                else straightCount++;
            }

            return classes;
        }

        /// <summary>
        /// True when segment <paramref name="index"/> is an arc. An out of range index is the
        /// missing neighbour at the end of an open polyline and is not an arc. Coincident,
        /// Point and Empty segments are degenerate rather than curved, so a duplicate vertex
        /// counts as a straight vertex.
        /// </summary>
        private static bool IsArcSegment(Polyline pline, int index, int segmentCount)
        {
            if (index < 0 || index >= segmentCount) return false;

            return pline.GetSegmentType(index) == SegmentType.Arc;
        }

        /// <summary>
        /// Sets the traits for one vertex class once, then draws every vertex in that class.
        /// Does nothing when the class is empty, which keeps an all-straight polyline from
        /// paying for the arc style's linetype lookup on every regen.
        /// </summary>
        private static void DrawClass(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd,
            Polyline pline,
            VertexClass[] classes,
            VertexClass wanted,
            int count,
            MarkerStyle style,
            System.Drawing.Color polylineRgb,
            LineWeight polylineLineWeight)
        {
            if (count <= 0) return;

            double radius = style.Radius;
            if (radius <= 0.0) return;

            wd.SubEntityTraits.TrueColor = MarkerColor(style, polylineRgb);
            wd.SubEntityTraits.LineWeight =
                ScaledLineWeight(polylineLineWeight, style.LineWeightFactor);

            ObjectId linetypeId = LinetypeResolver.Resolve(pline.Database, style.Linetype);
            if (!linetypeId.IsNull) wd.SubEntityTraits.LineType = linetypeId;

            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] != wanted) continue;

                wd.Geometry.Circle(pline.GetPoint3dAt(i), radius, Vector3d.ZAxis);
            }
        }

        /// <summary>
        /// A highly visible colour that is clearly distinct from the colour the polyline is
        /// drawn with: the complementary hue, forced to full saturation and full brightness.
        /// Rotating the hue guarantees the marker cannot be confused with the polyline, while
        /// pinning saturation and brightness keeps it vivid regardless of how dark or washed
        /// out the polyline is - a plain RGB negative would turn white polylines black, which
        /// is invisible on the usual dark background.
        /// Achromatic polylines (white, black, grey) have no hue to complement and get
        /// <see cref="AchromaticMarkerColor"/> instead.
        /// </summary>
        private static EntityColor MarkerColor(MarkerStyle style, System.Drawing.Color polylineRgb)
        {
            if (style.ColorMode == MarkerColorMode.FixedColor)
            {
                System.Drawing.Color fixedColor = HtmlColor.Parse(style.FixedColor);
                return new EntityColor(fixedColor.R, fixedColor.G, fixedColor.B);
            }

            if (polylineRgb.GetSaturation() < AchromaticSaturationLimit)
                return AchromaticMarkerColor;

            return FullySaturatedFromHue((polylineRgb.GetHue() + 180.0) % 360.0);
        }

        /// <summary>
        /// The RGB the polyline is actually drawn in.
        ///
        /// The graphics system does NOT hand the overrule a resolved colour - for a ByLayer
        /// entity the traits still read back ByLayer/256 (verified against a live drawing),
        /// which is why this walks the chain itself: traits first when they carry something
        /// concrete, then the entity's own colour, then the layer's. Only when all three come
        /// up empty does it fall back to <see cref="FallbackColorIndex"/>.
        /// </summary>
        private static System.Drawing.Color EffectiveRgb(
            Polyline pline, Autodesk.AutoCAD.GraphicsInterface.SubEntityTraits traits)
        {
            EntityColor fromTraits = traits.TrueColor;

            if (fromTraits.ColorMethod == ColorMethod.ByColor)
                return System.Drawing.Color.FromArgb(
                    fromTraits.Red, fromTraits.Green, fromTraits.Blue);

            if (fromTraits.ColorMethod == ColorMethod.ByAci &&
                IsConcreteAci(fromTraits.ColorIndex))
                return AciRgb(fromTraits.ColorIndex);

            Autodesk.AutoCAD.Colors.Color entityColor = pline.Color;
            if (!entityColor.IsByLayer && !entityColor.IsByBlock)
                return entityColor.ColorValue;

            System.Drawing.Color? fromLayer = LayerRgb(pline);
            if (fromLayer.HasValue) return fromLayer.Value;

            return AciRgb(FallbackColorIndex);
        }

        /// <summary>
        /// The colour of the polyline's layer, or null when it cannot be read. Uses an
        /// open-close transaction rather than a cache: a layer's colour can change while the
        /// overrule is on, and a stale marker colour is harder to explain than the lookup is
        /// expensive.
        /// </summary>
        private static System.Drawing.Color? LayerRgb(Polyline pline)
        {
            try
            {
                ObjectId layerId = pline.LayerId;
                Database? db = layerId.Database ?? pline.Database;
                if (db == null || layerId.IsNull) return null;

                using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    if (tr.GetObject(layerId, OpenMode.ForRead) is not LayerTableRecord layer)
                        return null;

                    Autodesk.AutoCAD.Colors.Color color = layer.Color;
                    if (color.IsByLayer || color.IsByBlock) return null;

                    return color.ColorValue;
                }
            }
            catch (System.Exception)
            {
                //Never let a colour lookup break the drawing of the entity.
                return null;
            }
        }

        private static bool IsConcreteAci(short aci) => aci >= 1 && aci <= 255;

        private static System.Drawing.Color AciRgb(short aci)
        {
            if (!IsConcreteAci(aci)) aci = FallbackColorIndex;

            return Autodesk.AutoCAD.Colors.Color
                .FromColorIndex(ColorMethod.ByAci, aci).ColorValue;
        }

        /// <summary>
        /// HSV to RGB for saturation = 1 and value = 1, which reduces to walking one channel
        /// up and one down through the six sectors of the colour wheel.
        /// </summary>
        private static EntityColor FullySaturatedFromHue(double hue)
        {
            double position = hue / 60.0;
            int sector = (int)Math.Floor(position) % 6;
            double fraction = position - Math.Floor(position);

            byte full = 255;
            byte rising = (byte)Math.Round(255.0 * fraction);
            byte falling = (byte)Math.Round(255.0 * (1.0 - fraction));
            byte none = 0;

            switch (sector)
            {
                case 0: return new EntityColor(full, rising, none);
                case 1: return new EntityColor(falling, full, none);
                case 2: return new EntityColor(none, full, rising);
                case 3: return new EntityColor(none, falling, full);
                case 4: return new EntityColor(rising, none, full);
                default: return new EntityColor(full, none, falling);
            }
        }

        /// <summary>
        /// The supported lineweight closest to <paramref name="factor"/> x <paramref name="current"/>.
        /// </summary>
        private static LineWeight ScaledLineWeight(LineWeight current, double factor)
        {
            int hundredthsMm = (int)current;
            if (hundredthsMm <= 0) hundredthsMm = FallbackLineWeightHundredthsMm;

            if (factor <= 0.0) factor = MarkerStyle.DefaultLineWeightFactor;

            int target = (int)Math.Round(hundredthsMm * factor);

            return ConcreteLineWeights
                .OrderBy(lw => Math.Abs((int)lw - target))
                .First();
        }
    }
}
