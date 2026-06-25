using Autodesk.AutoCAD.DatabaseServices;

namespace NorsynDistrictZones.Reactors;

/// <summary>
/// Collects ObjectIds of polylines appended to the database during a command, so
/// they can be processed at command-end (never mid-command). Pure bookkeeping —
/// it performs NO database writes (that would be illegal inside ObjectAppended).
/// </summary>
internal sealed class NewCurveCollector
{
    private readonly List<ObjectId> _appended = new();

    public void OnObjectAppended(object? sender, ObjectEventArgs e)
    {
        // Only track lightweight polylines on a real layer; zone containers and
        // everything else are ignored. Erased/duplicate filtering happens at drain.
        if (e.DBObject is Polyline pl && !pl.IsErased)
            _appended.Add(pl.ObjectId);
    }

    /// <summary>Take and clear the collected ids (called once at command-end).</summary>
    public IReadOnlyList<ObjectId> Drain()
    {
        var copy = _appended.Where(id => !id.IsNull && !id.IsErased).Distinct().ToList();
        _appended.Clear();
        return copy;
    }

    public void Clear() => _appended.Clear();
    public int PendingCount => _appended.Count;
}
