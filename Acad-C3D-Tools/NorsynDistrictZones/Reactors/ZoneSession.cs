using Autodesk.AutoCAD.DatabaseServices;

using NetTopologySuite.Geometries;

using NorsynDistrictZones.Topology;

namespace NorsynDistrictZones.Reactors;

/// <summary>
/// In-memory subdivision state for one drawing: the live faces and the container
/// entity that renders each. Source of truth within a session for classification
/// (which face contains a point/region) and for split/hole/merge edits.
/// (Cross-session persistence — reading containers back on open — is a later refinement.)
/// </summary>
internal sealed class ZoneSession
{
    public sealed class Entry
    {
        public ObjectId Container;
        public ZoneFace Face;
        public Entry(ObjectId container, ZoneFace face) { Container = container; Face = face; }
    }

    private static readonly Dictionary<Database, ZoneSession> Sessions = new();

    public static ZoneSession For(Database db)
    {
        if (!Sessions.TryGetValue(db, out var s)) { s = new ZoneSession(); Sessions[db] = s; }
        return s;
    }

    public static void Forget(Database db) => Sessions.Remove(db);

    private readonly List<Entry> _entries = new();
    public IReadOnlyList<Entry> Entries => _entries;

    public Entry Add(ObjectId container, ZoneFace face)
    {
        var e = new Entry(container, face);
        _entries.Add(e);
        return e;
    }

    public void Remove(Entry e) => _entries.Remove(e);
    public void Clear() => _entries.Clear();

    public int NextNumber() => _entries.Count == 0 ? 1 : _entries.Max(e => e.Face.Number) + 1;

    /// <summary>The face whose polygon contains the given point (holes excluded), or null.</summary>
    public Entry? FaceAt(Coordinate point) =>
        _entries.FirstOrDefault(e => e.Face.Polygon.Contains(e.Face.Polygon.Factory.CreatePoint(point)));

    /// <summary>The face that wholly contains the given closed region, or null.</summary>
    public Entry? FaceContaining(Polygon region) =>
        _entries.FirstOrDefault(e => e.Face.Polygon.Contains(region.InteriorPoint));
}
