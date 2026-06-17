namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Per-document German-pipe session state. Currently just the active DN selected in
/// the DE palette, which drives PDDRAW. Kept as a per-document class (mirroring
/// <c>PipePlanState</c>) to leave room for more session state later.
/// </summary>
internal sealed class PipePlanDEState : IDisposable
{
    /// <summary>The DN chosen in the DE palette; null until the user picks one.</summary>
    public int? ActiveDn { get; set; }

    public void Dispose()
    {
    }
}
