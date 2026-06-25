namespace NorsynDistrictZones.Topology;

/// <summary>
/// Stable identity for one zone (face). Persisted as XData on the rendered
/// MPolygon so a zone keeps its number, name and colour across sessions even as
/// the subdivision is edited. A GUID is used so splits/merges can mint new ids
/// without colliding with existing ones.
/// </summary>
public readonly record struct ZoneId(Guid Value)
{
    public static ZoneId New(Func<Guid> guidFactory) => new(guidFactory());
    public override string ToString() => Value.ToString("N");
    public static bool TryParse(string? s, out ZoneId id)
    {
        if (Guid.TryParse(s, out var g)) { id = new ZoneId(g); return true; }
        id = default;
        return false;
    }
}
