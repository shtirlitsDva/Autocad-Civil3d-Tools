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
    /// The circle is styled relative to the polyline it belongs to:
    /// - Radius is a fixed setting, it does not follow the polyline width.
    /// - Colour is either a fully saturated marker colour on the complementary hue of the
    ///   polyline's own colour, or one fixed ACI, see <see cref="MarkerColor"/>.
    /// - Lineweight is a configurable multiple of the polyline's lineweight.
    /// </summary>
    public class PolylineVertexCircles : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
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

            //Read the polyline's traits before overwriting them
            EntityColor markerColor = MarkerColor(pline, wd.SubEntityTraits, settings);
            LineWeight thickerLineWeight = ScaledLineWeight(
                wd.SubEntityTraits.LineWeight, settings.LineWeightFactor);

            wd.SubEntityTraits.TrueColor = markerColor;
            wd.SubEntityTraits.LineWeight = thickerLineWeight;

            double radius = settings.Radius;
            if (radius <= 0.0) return true;

            for (int i = 0; i < pline.NumberOfVertices; i++)
                wd.Geometry.Circle(pline.GetPoint3dAt(i), radius, Vector3d.ZAxis);

            return true;
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
        private static EntityColor MarkerColor(
            Polyline pline,
            Autodesk.AutoCAD.GraphicsInterface.SubEntityTraits traits,
            VertexCirclesSettings settings)
        {
            if (settings.ColorMode == MarkerColorMode.FixedColor)
            {
                System.Drawing.Color fixedColor = HtmlColor.Parse(settings.FixedColor);
                return new EntityColor(fixedColor.R, fixedColor.G, fixedColor.B);
            }

            System.Drawing.Color source = EffectiveRgb(pline, traits);

            if (source.GetSaturation() < AchromaticSaturationLimit)
                return AchromaticMarkerColor;

            return FullySaturatedFromHue((source.GetHue() + 180.0) % 360.0);
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

            if (factor <= 0.0) factor = VertexCirclesSettings.DefaultLineWeightFactor;

            int target = (int)Math.Round(hundredthsMm * factor);

            return ConcreteLineWeights
                .OrderBy(lw => Math.Abs((int)lw - target))
                .First();
        }
    }
}
