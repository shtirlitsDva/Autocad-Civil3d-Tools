using System.Collections.Generic;
using System.Linq;

namespace AcadOverrules.VertexCircles
{
    /// <summary>
    /// How the vertex circle picks its colour.
    /// </summary>
    public enum MarkerColorMode
    {
        /// <summary>
        /// Fully saturated complementary hue of the polyline's own colour, so every marker
        /// is vivid and cannot be confused with the polyline it belongs to.
        /// </summary>
        ComplementaryHue = 0,

        /// <summary>
        /// One fixed colour for all markers, see <see cref="MarkerStyle.FixedColor"/>.
        /// </summary>
        FixedColor = 1,
    }

    /// <summary>
    /// The look of the circle drawn at one class of vertex. A profile holds two of these,
    /// see <see cref="VertexCirclesSettings"/>.
    /// </summary>
    public sealed class MarkerStyle
    {
        /// <summary>Circle radius in drawing units.</summary>
        public double Radius { get; set; } = DefaultRadius;

        public MarkerColorMode ColorMode { get; set; } = MarkerColorMode.ComplementaryHue;

        /// <summary>
        /// HTML colour used when <see cref="ColorMode"/> is
        /// <see cref="MarkerColorMode.FixedColor"/>, e.g. "#FF00FF".
        /// </summary>
        public string FixedColor { get; set; } = DefaultFixedColor;

        /// <summary>Circle lineweight relative to the lineweight of the polyline.</summary>
        public double LineWeightFactor { get; set; } = DefaultLineWeightFactor;

        /// <summary>
        /// Name of the linetype the circle is drawn with. Resolved against the linetype table
        /// of the drawing the polyline lives in, so a profile carried to a drawing that does
        /// not have the linetype loaded falls back to <see cref="DefaultLinetype"/>.
        /// </summary>
        public string Linetype { get; set; } = DefaultLinetype;

        public const double DefaultRadius = 0.05;
        public const string DefaultFixedColor = "#FF00FF";
        public const double DefaultLineWeightFactor = 2.0;

        /// <summary>Present in every drawing, so it is always resolvable.</summary>
        public const string DefaultLinetype = "Continuous";

        /// <summary>
        /// Default radius for arc vertices. Twice <see cref="DefaultRadius"/>, so the two
        /// classes are already distinguishable before the user configures anything - a
        /// feature whose whole point is telling them apart should not ship looking identical.
        /// </summary>
        public const double DefaultArcRadius = 0.1;

        public static MarkerStyle ForStraightVertices() => new MarkerStyle();

        public static MarkerStyle ForArcVertices() =>
            new MarkerStyle { Radius = DefaultArcRadius };

        public MarkerStyle Clone() =>
            new MarkerStyle
            {
                Radius = Radius,
                ColorMode = ColorMode,
                FixedColor = FixedColor,
                LineWeightFactor = LineWeightFactor,
                Linetype = Linetype,
            };
    }

    /// <summary>
    /// Everything <see cref="PolylineVertexCircles"/> needs to draw. Plain serialisable state -
    /// no behaviour, no AutoCAD types, so it can be round tripped through JSON as is.
    ///
    /// A vertex is classified by the segments touching it, and each class gets its own style:
    /// a vertex whose neighbouring segments are all lines is a straight vertex, a vertex with
    /// at least one arc neighbour is an arc vertex. Degenerate segments (Coincident, Point,
    /// Empty) are not arcs, so a duplicate vertex counts as straight.
    /// </summary>
    public sealed class VertexCirclesSettings
    {
        /// <summary>The circle drawn where only line segments meet.</summary>
        public MarkerStyle StraightVertex { get; set; } = MarkerStyle.ForStraightVertices();

        /// <summary>The circle drawn where at least one arc segment meets the vertex.</summary>
        public MarkerStyle ArcVertex { get; set; } = MarkerStyle.ForArcVertices();

        /// <summary>
        /// Layer names and layer name masks the overrule applies to. AutoCAD wildcards are
        /// supported, so <c>0-FJV-*</c> works the same way it does in the layer manager.
        /// An empty list means "every layer".
        /// </summary>
        public List<string> LayerFilters { get; set; } = new List<string>();

        public VertexCirclesSettings Clone() =>
            new VertexCirclesSettings
            {
                StraightVertex = StraightVertex.Clone(),
                ArcVertex = ArcVertex.Clone(),
                LayerFilters = LayerFilters.ToList(),
            };
    }

    /// <summary>
    /// A named set of settings the user can switch between.
    /// </summary>
    public sealed class VertexCirclesProfile
    {
        public string Name { get; set; } = DefaultProfileName;

        public VertexCirclesSettings Settings { get; set; } = new VertexCirclesSettings();

        public const string DefaultProfileName = "Default";

        public VertexCirclesProfile Clone() =>
            new VertexCirclesProfile { Name = Name, Settings = Settings.Clone() };
    }

    /// <summary>
    /// The whole persisted document: every profile plus which one is in use.
    /// </summary>
    public sealed class VertexCirclesConfig
    {
        public string ActiveProfileName { get; set; } = VertexCirclesProfile.DefaultProfileName;

        public List<VertexCirclesProfile> Profiles { get; set; } = new List<VertexCirclesProfile>();

        /// <summary>
        /// The active profile. Falls back to the first profile, and creates a default one when
        /// the config is empty, so callers never have to null check.
        /// Derived from <see cref="ActiveProfileName"/> - not persisted, or the file would
        /// carry a second copy of the profile.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public VertexCirclesProfile ActiveProfile
        {
            get
            {
                if (Profiles.Count == 0)
                    Profiles.Add(new VertexCirclesProfile());

                return Profiles.FirstOrDefault(
                    p => string.Equals(p.Name, ActiveProfileName, System.StringComparison.OrdinalIgnoreCase))
                    ?? Profiles[0];
            }
        }

        public static VertexCirclesConfig CreateDefault()
        {
            var config = new VertexCirclesConfig();
            config.Profiles.Add(new VertexCirclesProfile());
            return config;
        }
    }
}
