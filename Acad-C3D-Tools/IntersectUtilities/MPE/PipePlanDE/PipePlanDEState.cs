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

    /// <summary>Excavation depth band chosen alongside the DN; selects B vs B1 for the
    /// trench. Defaults to Shallow (≤ 1.3 m → B), the common case.</summary>
    public PipePlanDETrenchDepth ActiveDepth { get; set; } = PipePlanDETrenchDepth.Shallow;

    public void Dispose()
    {
    }
}
