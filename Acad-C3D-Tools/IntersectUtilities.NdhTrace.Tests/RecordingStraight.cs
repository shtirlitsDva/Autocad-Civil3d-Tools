using IntersectUtilities.UtilsCommon.Enums;

using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>One question the route builder put to the new pipeline, as it put it.</summary>
internal readonly record struct Ask(
    LegacyIdentitySpan Before, LegacyIdentitySpan After, double TurnDegrees, bool TurnedByAPart)
{
    public (PipeSystemEnum System, bool Twin, int Dn) BeforeId => Before.Identity;
    public (PipeSystemEnum System, bool Twin, int Dn) AfterId => After.Identity;
    public bool IsPipeRunningThrough => BeforeId == AfterId;

    public override string ToString() =>
        $"{Before.System} {Before.Type} DN{Before.Dn} -> {After.System} {After.Type} DN{After.Dn} " +
        $"@ {TurnDegrees:F3} deg, byPart={TurnedByAPart}";
}

/// <summary>
/// THE REACH DOOR, HELD OPEN AND WATCHED. Law R1 of NDH's reach-and-radius.md
/// says the route builder must READ how much of a run a thing occupies from
/// that thing, never keep a length of its own - so the only way to test the
/// law is to be the thing, answer known metres, and check that exactly those
/// metres came back out of the route.
/// <para>
/// Every question is recorded in the order it was asked, which is the only
/// window a test has onto which PIPE the builder thought stood on a vertex.
/// </para>
/// </summary>
internal sealed class RecordingStraight : INdhPartStraight
{
    private readonly Func<LegacyIdentitySpan, LegacyIdentitySpan, double, bool, PartStraight> answer;

    public RecordingStraight(
        Func<LegacyIdentitySpan, LegacyIdentitySpan, double, bool, PartStraight> answer) =>
        this.answer = answer;

    /// <summary>
    /// The usual shape: a part that TURNS a vertex reaches
    /// <paramref name="partLeg"/> either way, a pipe bent elastically reaches
    /// <paramref name="bendSetback"/>, a change on open straight reaches
    /// <paramref name="changeReach"/>, and the drawing's shortest pipe is
    /// <paramref name="minimumPipe"/> - answered at EVERY vertex, as NDH
    /// answers it, because it is a setting and not a fact about the vertex.
    /// </summary>
    public static RecordingStraight Reaching(
        double partLeg, double bendSetback = 0.0, double changeReach = 0.0,
        double minimumPipe = 0.0) =>
        new RecordingStraight((before, after, turn, byPart) =>
            byPart ? new PartStraight(partLeg, partLeg, minimumPipe)
            : turn > 0.0 ? new PartStraight(bendSetback, bendSetback, minimumPipe)
            : new PartStraight(changeReach, changeReach, minimumPipe));

    public List<Ask> Asks { get; } = new List<Ask>();

    public PartStraight At(
        LegacyIdentitySpan before, LegacyIdentitySpan after, double turnDegrees, bool turnedByAPart)
    {
        Asks.Add(new Ask(before, after, turnDegrees, turnedByAPart));
        return answer(before, after, turnDegrees, turnedByAPart);
    }

    /// <summary>
    /// THE ONE ASK PER VERTEX THAT <c>SizeBends</c> PUTS, index-aligned with the
    /// built route's vertices.
    /// <para>
    /// <c>SizeBends</c> is the last thing in <c>Build</c> that asks, and it opens
    /// by walking every vertex in order and asking each exactly once (its second
    /// loop, which settles the radii, asks nothing). So the final
    /// <paramref name="vertexCount"/> questions ARE that walk, and the i-th of
    /// them is the i-th vertex of the route that came back. This is how a test
    /// sees which PIPE the builder believed stood on a vertex - state no route
    /// otherwise carries out of the call.
    /// </para>
    /// </summary>
    public IReadOnlyList<Ask> PerVertex(int vertexCount)
    {
        Assert.True(Asks.Count >= vertexCount,
            $"Only {Asks.Count} asks were recorded; the per-vertex walk needs {vertexCount}.");
        return Asks.Skip(Asks.Count - vertexCount).ToList();
    }

    public int Count(LegacyIdentitySpan before, LegacyIdentitySpan after) =>
        Asks.Count(a => a.BeforeId == before.Identity && a.AfterId == after.Identity);
}
