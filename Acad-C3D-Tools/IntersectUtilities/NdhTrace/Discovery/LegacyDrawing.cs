using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>A legacy branch: one pipeline leaving another through a legacy part.</summary>
/// <param name="MainName">The legacy pipeline the branch leaves.</param>
/// <param name="BranchName">The legacy pipeline that leaves it.</param>
/// <param name="Navn">The legacy part's block name (its real name).</param>
/// <param name="BlockHandle">The legacy part's handle in the source drawing.</param>
/// <param name="BranchPort">Where the branch pipe meets the part: its branch port(s).</param>
/// <param name="Site">The part's place on the main's centreline.</param>
/// <param name="MainSystem">The main's system at the site, as the legacy size array says it.</param>
/// <param name="MainType">The main's type at the site.</param>
/// <param name="NamedBy">Where the branch pipeline's name came from, for the report.</param>
internal sealed record LegacyBranch(
    string MainName,
    string BranchName,
    string Navn,
    string BlockHandle,
    Point2d BranchPort,
    Point2d Site,
    PipeSystemEnum MainSystem,
    PipeTypeEnum MainType,
    string NamedBy);

/// <summary>
/// A legacy branch the reading could not make whole (no branch pipeline named,
/// no part joining two pipelines that meet). Never connected; always marked.
/// </summary>
/// <param name="What">The pipelines or the part, for the report.</param>
/// <param name="Site">Where to mark it.</param>
/// <param name="Note">English, for the drawing's marker.</param>
/// <param name="Reason">Danish, for the report.</param>
internal sealed record LegacyBranchProblem(string What, Point2d Site, string Note, string Reason);

/// <summary>
/// Two legacy pipelines meeting end to end with no branch part there.
/// <paramref name="AAtStart"/> says which end of <paramref name="A"/> meets
/// (its first vertex when true), <paramref name="BAtStart"/> likewise.
/// </summary>
internal readonly record struct LegacyJoin(string A, bool AAtStart, string B, bool BAtStart, Point2d At);

/// <summary>
/// Everything NDHFROMFJV takes from the legacy drawing, read in one pass and
/// independent of it afterwards: the traces are in memory, not database
/// resident, so the source drawing is closed before anything is written.
/// </summary>
internal sealed class LegacyDrawing : IDisposable
{
    public LegacyDrawing(LegacyTraceResult traces) => Traces = traces;

    public LegacyTraceResult Traces { get; }
    public List<LegacyJoin> Joins { get; } = new List<LegacyJoin>();
    public List<LegacyBranch> Branches { get; } = new List<LegacyBranch>();
    public List<LegacyBranchProblem> Problems { get; } = new List<LegacyBranchProblem>();
    /// <summary>Service connections (stik), by main pipeline: never connected by this import.</summary>
    public SortedDictionary<string, int> ServiceConnections { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
    public LegacySettingsFacts Settings { get; } = new LegacySettingsFacts();
    /// <summary>Distance from the network's root (the largest DN), by pipeline name.</summary>
    public Dictionary<string, int> Depth { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
    /// <summary>What the reading had to say about the network itself (Danish).</summary>
    public List<string> NetworkNotes { get; } = new List<string>();

    /// <summary>A pipeline the network did not reach is as far from the root as can be.</summary>
    public int DepthOf(string name) => Depth.TryGetValue(name, out int d) ? d : int.MaxValue;

    public void Dispose() => Traces.Dispose();
}
