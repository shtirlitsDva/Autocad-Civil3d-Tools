using NetTopologySuite.Geometries;

namespace NorsynDistrictZones.Topology;

/// <summary>
/// One zone = one face of the planar subdivision. Geometry is an NTS polygon
/// (supports a hole when a closed cut is made inside it). Identity (number, name,
/// stable colour) is persisted; the polygon is the editable shape.
/// </summary>
public sealed class ZoneFace
{
    public ZoneId Id { get; }
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable random fill colour as ARGB; persisted so it never changes after creation.</summary>
    public int ColorArgb { get; set; }

    /// <summary>The face geometry (outer ring, optionally with holes), in WCS.</summary>
    public Polygon Polygon { get; set; }

    public ZoneFace(ZoneId id, Polygon polygon)
    {
        Id = id;
        Polygon = polygon ?? throw new ArgumentNullException(nameof(polygon));
    }

    /// <summary>Label anchor — the polygon's interior point (always inside, even for concave/holed faces).</summary>
    public Coordinate LabelPoint => Polygon.InteriorPoint.Coordinate;
}
