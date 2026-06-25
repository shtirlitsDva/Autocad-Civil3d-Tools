using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Overlay;     // SpatialFunction
using NetTopologySuite.Operation.OverlayNG;   // OverlayNG (snap-rounding under a fixed PrecisionModel)

namespace NorsynDistrictZones.Topology;

/// <summary>
/// The single source of NTS precision for ALL zone geometry. Every zone coordinate is
/// snapped to a fixed 1&nbsp;mm grid, and every overlay (union / difference / intersection)
/// runs through <see cref="OverlayNG"/> at that same fixed <see cref="PrecisionModel"/>,
/// which makes NTS use its snap-rounding noder.
///
/// <para><b>Why this exists.</b> Zone polygons live in UTM (~6.19&nbsp;million). At that
/// magnitude a <c>double</c> resolves only ~1e-9&nbsp;m, so an interactive OSnap lands a cut
/// endpoint ~0.3&nbsp;µm off the boundary — a <i>touch</i>, not a <i>crossing</i>. The default
/// floating-precision noder then fails to divide the polygon (the Polygonizer returns the
/// original single face) and a perfectly-snapped split silently does nothing. Forcing a fixed
/// grid makes the noder snap those near-coincident points together → the split is robust.</para>
///
/// <para><b>Why it is safe.</b> Sub-millimetre fidelity is irrelevant for zone pricing, and a
/// fixed ABSOLUTE grid is idempotent: a coordinate already on the grid never moves again, so
/// repeated split / merge / clip do not accumulate drift. Verified live on UTM data: split →
/// 2 faces at every scale 1e2..1e6, and 0&nbsp;m² area drift across 6 split→merge→split cycles
/// (one-time snap of 0.84&nbsp;m² on a 3.58&nbsp;million&nbsp;m² zone, then perfectly stable).</para>
/// </summary>
internal static class NdzGeometry
{
    /// <summary>Grid cell size in metres. 1&nbsp;mm — tune here only.</summary>
    public const double GridSize = 0.001;

    /// <summary>Fixed precision model (scale = 1/grid). Drives the snap-rounding noder.</summary>
    public static readonly PrecisionModel Precision = new(1.0 / GridSize);

    /// <summary>The factory every NDZ geometry is built with, so all coordinates share the grid.</summary>
    public static readonly GeometryFactory Factory = new(Precision);

    /// <summary>Round a coordinate onto the zone grid, in place.</summary>
    public static void Snap(Coordinate c) => Precision.MakePrecise(c);

    // Robust fixed-precision overlays — the ONLY overlay path NDZ uses. The fixed
    // PrecisionModel routes these through the snap-rounding noder (see class remarks).
    public static Geometry Union(Geometry a, Geometry b) => OverlayNG.Overlay(a, b, SpatialFunction.Union, Precision);
    public static Geometry Difference(Geometry a, Geometry b) => OverlayNG.Overlay(a, b, SpatialFunction.Difference, Precision);
    public static Geometry Intersection(Geometry a, Geometry b) => OverlayNG.Overlay(a, b, SpatialFunction.Intersection, Precision);
}
