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
    /// How an overridden segment picks its colour.
    /// </summary>
    public enum SegmentColorMode
    {
        /// <summary>The colour the polyline itself is drawn with - no change.</summary>
        Polyline = 0,

        /// <summary>
        /// Fully saturated complementary hue of the polyline's own colour, the same rule the
        /// vertex circles use, see <see cref="MarkerColorMode.ComplementaryHue"/>.
        /// </summary>
        ComplementaryHue = 1,

        /// <summary>One fixed colour, see <see cref="SegmentStyle.FixedColor"/>.</summary>
        FixedColor = 2,
    }

    /// <summary>
    /// The traits of a line drawn in relation to the polyline: colour, lineweight and
    /// linetype, each with a "same as the polyline" value. Every property defaults to that
    /// value, so a fresh style looks exactly like the polyline.
    /// </summary>
    public class LineStyle
    {
        public SegmentColorMode ColorMode { get; set; } = SegmentColorMode.Polyline;

        /// <summary>
        /// HTML colour used when <see cref="ColorMode"/> is
        /// <see cref="SegmentColorMode.FixedColor"/>, e.g. "#00FFFF".
        /// </summary>
        public string FixedColor { get; set; } = DefaultFixedColor;

        /// <summary>
        /// Lineweight relative to the lineweight of the polyline. Exactly 1 keeps the
        /// polyline's lineweight trait as it is, ByLayer included.
        /// </summary>
        public double LineWeightFactor { get; set; } = DefaultLineWeightFactor;

        /// <summary>
        /// Name of the linetype the line is drawn with. Empty means the polyline's own
        /// linetype. A name that is not loaded in the drawing on screen resolves to Continuous,
        /// see <see cref="LinetypeResolver"/>.
        /// </summary>
        public string Linetype { get; set; } = PolylineLinetype;

        public const string DefaultFixedColor = "#00FFFF";
        public const double DefaultLineWeightFactor = 1.0;

        /// <summary>The empty name: draw with the linetype of the polyline itself.</summary>
        public const string PolylineLinetype = "";

        /// <summary>True when <see cref="Linetype"/> means "the polyline's own".</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool UsesPolylineLinetype => string.IsNullOrWhiteSpace(Linetype);

        public LineStyle Clone() => CopyTo(new LineStyle());

        protected T CopyTo<T>(T target) where T : LineStyle
        {
            target.ColorMode = ColorMode;
            target.FixedColor = FixedColor;
            target.LineWeightFactor = LineWeightFactor;
            target.Linetype = Linetype;
            return target;
        }
    }

    /// <summary>
    /// How one class of segment (straight or arc) is drawn when the profile overrides it.
    /// Unlike a vertex circle, which is extra geometry on top of the polyline, an overridden
    /// segment replaces the polyline's own graphics for that segment: the overrule draws the
    /// segment itself with these traits instead of letting the polyline draw it.
    /// Every property defaults to "same as the polyline", so a freshly enabled override
    /// changes nothing until the user picks something.
    /// </summary>
    public sealed class SegmentStyle : LineStyle
    {
        /// <summary>
        /// Off: the polyline draws this class of segment as it always has. On: the overrule
        /// draws it with the traits of the <see cref="LineStyle"/>.
        /// </summary>
        public bool Override { get; set; } = false;

        public new SegmentStyle Clone()
        {
            SegmentStyle copy = CopyTo(new SegmentStyle());
            copy.Override = Override;
            return copy;
        }
    }

    /// <summary>
    /// The tangent intersection of an arc segment: the point where the tangents at the
    /// arc's start and end meet - the corner the two straight segments would have made if
    /// the arc were not there (the PI of a road or pipe bend). When shown, each arc gets two
    /// lines, arc start to corner and corner to arc end, and a circle at the corner. The
    /// corner is also an object snap point (Center and Node), see
    /// <see cref="PolylineTangentIntersectionSnap"/>.
    ///
    /// Arcs of about 177 degrees and more are skipped: at 180 the tangents are parallel and
    /// beyond it the corner lies behind the arc.
    /// </summary>
    public sealed class TangentIntersectionStyle
    {
        /// <summary>Off by default, so existing profiles draw exactly as before.</summary>
        public bool Show { get; set; } = false;

        /// <summary>The two tangent lines. Default: thin and dashed, in the polyline's colour.</summary>
        public LineStyle Lines { get; set; } = DefaultLines();

        /// <summary>The circle at the corner.</summary>
        public MarkerStyle Point { get; set; } = DefaultPoint();

        /// <summary>
        /// Loaded into the drawing when the feature is switched on, see
        /// <see cref="LinetypeLoader"/>, because a drawing does not have it by default.
        /// </summary>
        public const string DefaultLinetype = "DASHED";

        /// <summary>Half the polyline's lineweight: a construction line, not a pipe.</summary>
        public const double DefaultLineWeightFactor = 0.5;

        /// <summary>
        /// Bigger than the arc vertex circle (<see cref="MarkerStyle.DefaultArcRadius"/>) so
        /// the corner is not mistaken for a vertex.
        /// </summary>
        public const double DefaultPointRadius = 0.15;

        public static LineStyle DefaultLines() =>
            new LineStyle
            {
                LineWeightFactor = DefaultLineWeightFactor,
                Linetype = DefaultLinetype,
            };

        public static MarkerStyle DefaultPoint() =>
            new MarkerStyle
            {
                Radius = DefaultPointRadius,
                LineWeightFactor = DefaultLineWeightFactor,
            };

        public TangentIntersectionStyle Clone() =>
            new TangentIntersectionStyle
            {
                Show = Show,
                Lines = Lines.Clone(),
                Point = Point.Clone(),
            };
    }

    /// <summary>
    /// Everything <see cref="PolylineVertexCircles"/> needs to draw. Plain serialisable state -
    /// no behaviour, no AutoCAD types, so it can be round tripped through JSON as is.
    ///
    /// Vertices: a vertex is classified by the segments touching it, and each class gets its
    /// own circle style. A vertex is an arc vertex when it is the start vertex of an arc
    /// segment and <see cref="ArcVertexAtArcStart"/> is on, or the end vertex of an arc
    /// segment and <see cref="ArcVertexAtArcEnd"/> is on. Every other vertex is a straight
    /// vertex. Degenerate segments (Coincident, Point, Empty) are not arcs, so a duplicate
    /// vertex counts as straight.
    ///
    /// Segments: each class of segment can have its own <see cref="SegmentStyle"/> that
    /// replaces the polyline's own graphics for that class.
    ///
    /// Tangent intersections: optionally the corner of every arc segment is drawn, see
    /// <see cref="TangentIntersectionStyle"/>.
    /// </summary>
    public sealed class VertexCirclesSettings
    {
        /// <summary>The circle drawn at a straight vertex.</summary>
        public MarkerStyle StraightVertex { get; set; } = MarkerStyle.ForStraightVertices();

        /// <summary>The circle drawn at an arc vertex.</summary>
        public MarkerStyle ArcVertex { get; set; } = MarkerStyle.ForArcVertices();

        /// <summary>
        /// The vertex where an arc segment begins (its outgoing segment is the arc) counts as
        /// an arc vertex.
        /// </summary>
        public bool ArcVertexAtArcStart { get; set; } = true;

        /// <summary>
        /// The vertex where an arc segment ends (its incoming segment is the arc) counts as an
        /// arc vertex. With both this and <see cref="ArcVertexAtArcStart"/> on, every vertex
        /// touching an arc is an arc vertex, which is how the feature first shipped. Turning
        /// one of them off tells a straight segment between two arcs apart from a third arc:
        /// only one of its two end vertices is then marked as arc.
        /// </summary>
        public bool ArcVertexAtArcEnd { get; set; } = true;

        /// <summary>How line segments are drawn, when overridden.</summary>
        public SegmentStyle StraightSegment { get; set; } = new SegmentStyle();

        /// <summary>How arc segments are drawn, when overridden.</summary>
        public SegmentStyle ArcSegment { get; set; } = new SegmentStyle();

        /// <summary>The corner of every arc segment, see <see cref="TangentIntersectionStyle"/>.</summary>
        public TangentIntersectionStyle TangentIntersections { get; set; } = new TangentIntersectionStyle();

        /// <summary>
        /// Layer names and layer name masks the overrule applies to. AutoCAD wildcards are
        /// supported, so <c>0-FJV-*</c> works the same way it does in the layer manager.
        /// An empty list means "every layer".
        /// </summary>
        public List<string> LayerFilters { get; set; } = new List<string>();

        /// <summary>True when at least one segment class replaces the polyline's own graphics.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool OverridesAnySegment => StraightSegment.Override || ArcSegment.Override;

        public VertexCirclesSettings Clone() =>
            new VertexCirclesSettings
            {
                StraightVertex = StraightVertex.Clone(),
                ArcVertex = ArcVertex.Clone(),
                ArcVertexAtArcStart = ArcVertexAtArcStart,
                ArcVertexAtArcEnd = ArcVertexAtArcEnd,
                StraightSegment = StraightSegment.Clone(),
                ArcSegment = ArcSegment.Clone(),
                TangentIntersections = TangentIntersections.Clone(),
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
