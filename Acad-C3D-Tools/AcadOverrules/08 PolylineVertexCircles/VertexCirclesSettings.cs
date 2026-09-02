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
        /// One fixed colour for all markers, see <see cref="VertexCirclesSettings.FixedColor"/>.
        /// </summary>
        FixedColor = 1,
    }

    /// <summary>
    /// Everything <see cref="PolylineVertexCircles"/> needs to draw. Plain serialisable state -
    /// no behaviour, no AutoCAD types, so it can be round tripped through JSON as is.
    /// </summary>
    public sealed class VertexCirclesSettings
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
        /// Layer names and layer name masks the overrule applies to. AutoCAD wildcards are
        /// supported, so <c>0-FJV-*</c> works the same way it does in the layer manager.
        /// An empty list means "every layer".
        /// </summary>
        public List<string> LayerFilters { get; set; } = new List<string>();

        public const double DefaultRadius = 0.05;
        public const string DefaultFixedColor = "#FF00FF";
        public const double DefaultLineWeightFactor = 2.0;

        public VertexCirclesSettings Clone() =>
            new VertexCirclesSettings
            {
                Radius = Radius,
                ColorMode = ColorMode,
                FixedColor = FixedColor,
                LineWeightFactor = LineWeightFactor,
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
