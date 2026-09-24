using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// THE SITUATIONS THE DRAWING'S RULE SHEET DECIDES, by the token each situation
/// class states about itself. Core is the authority on this vocabulary and the
/// dbx refuses an unknown token BY NAME, so nothing here has to guess; these
/// six are named because they are the six the planner actually asks the sheet
/// about, and so the only six a caller can move by writing a row - or has to
/// read a row of, as the valve naming does.
///
/// Three other situations - Materialeskift, Endebund and Vertikal boejning -
/// will be ACCEPTED as rows and never read: each is resolved as the only
/// Produkt its role publishes. A row for one of them looks like policy and is
/// inert, which is why they are not here.
///
/// Afgreninger are not on the sheet at all. A branch is keyed by two systems
/// and a construction, which one row cannot state, so branch policy belongs to
/// the afgreningsmatrix and the import pins a branch's part per connection.
/// </summary>
internal static class NdhSituation
{
    /// <summary>A corner. Axis: deflectionDeg (0..180).</summary>
    public const string Elbow = "Elbow";

    /// <summary>A reduction. Axes: dnFrom, dnTo (both bores, open-topped).</summary>
    public const string Reducer = "Reducer";

    /// <summary>A bueroer. Axis: underElastic (a flag).</summary>
    public const string Arc = "Arc";

    /// <summary>A bonded/twin construction change. Axis: dn (a bore, open-topped).</summary>
    public const string Transition = "Transition";

    /// <summary>The same change in a square corner. Axis: dn.</summary>
    public const string CornerTransition = "CornerTransition";

    /// <summary>
    /// An authored valve. Axis: dn (a bore, open-topped). The row decides
    /// WHICH valve Produkt, never WHETHER there is a valve - that is authored.
    /// </summary>
    public const string Valve = "Valve";
}

/// <summary>
/// ONE BAND ON ONE AXIS, narrowing the row to part of its situation. The axis
/// id is the one the situation class publishes; a band naming an axis the
/// situation does not publish is REFUSED, never skipped, because a skipped band
/// would widen the row it was written to narrow.
///
/// The factories are how a band is spelled. An open upper end is the AXIS's own
/// zero rather than a flag here, and a flag axis is 1..1 - both are Core's
/// spellings, so a caller states what it means and never a magic number.
/// </summary>
internal readonly record struct NdhRuleBand(string Axis, double From, double To)
{
    /// <summary>From <paramref name="from"/> up to and including <paramref name="to"/>.</summary>
    public static NdhRuleBand Between(string axis, double from, double to) => new(axis, from, to);

    /// <summary>From <paramref name="from"/> upward with no upper end.</summary>
    public static NdhRuleBand AtLeast(string axis, double from) => new(axis, from, 0.0);

    /// <summary>A flag axis that must be set.</summary>
    public static NdhRuleBand Required(string axis) => new(axis, 1.0, 1.0);
}

/// <summary>
/// WHAT A ROW SELECTS. Two states, each its own type, because "this Produkt"
/// and "no part at all" are different answers and an empty name must never
/// stand in for the second. Each state spells its own wire value, so nothing
/// anywhere asks a selection what it is (kNsDhRule* in NsDhPipelineBridge.h).
/// </summary>
internal abstract record NdhRuleSelection
{
    /// <summary>kNsDhRule*: which of the two states this is.</summary>
    internal abstract int Selects { get; }

    /// <summary>The Produkt name, where the state has one.</summary>
    internal virtual string Produkt => "";
}

/// <summary>This situation uses this Produkt, named exactly as the catalogue publishes it.</summary>
internal sealed record NdhSelectsProdukt(string Name) : NdhRuleSelection
{
    internal override int Selects => 0;  //kNsDhRuleProdukt
    internal override string Produkt => Name;
}

/// <summary>This situation places nothing - a decision, not an absent name.</summary>
internal sealed record NdhSelectsNothing : NdhRuleSelection
{
    internal override int Selects => 1;  //kNsDhRuleNothing
}

/// <summary>
/// ONE ROW OF THE DRAWING'S FITTING RULE SHEET: for this pipe system, in this
/// situation, narrowed by these bands, use this. Nothing else - the ROLE is the
/// situation's own answer and is never stated twice.
/// </summary>
internal readonly record struct NdhFittingRule(
    string SystemToken,
    string SituationToken,
    IReadOnlyList<NdhRuleBand> Bands,
    NdhRuleSelection Selects)
{
    /// <summary>The whole situation, unnarrowed.</summary>
    public static NdhFittingRule Whole(string systemToken, string situationToken,
                                       NdhRuleSelection selects) =>
        new(systemToken, situationToken, System.Array.Empty<NdhRuleBand>(), selects);
}
