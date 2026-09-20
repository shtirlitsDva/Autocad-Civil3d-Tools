namespace IntersectUtilities.NdhTrace;

/// <summary>
/// WHO HEARS A VERDICT. The three things that can be said about a legacy block,
/// and the reason the verdicts are types rather than a nullable string: they do
/// not all end in the same place.
///
/// A caller implements only what it cares about - the census counts
/// <see cref="Translates"/> and ignores the rest; the marker places
/// <see cref="Missing"/> and ignores the rest - and neither of them ever asks a
/// verdict what kind it is.
/// </summary>
internal interface ILegacyVerdictAudience
{
    /// <summary>This block becomes this NDH Produkt.</summary>
    void Translates(string navn, string produkt);

    /// <summary>
    /// A real part NDH does not have yet. MARKED in the drawing with
    /// <paramref name="note"/> (English) and reported with
    /// <paramref name="reason"/> (Danish) - never silent.
    /// </summary>
    void Missing(string navn, string note, string reason);

    /// <summary>
    /// Nothing to do and nothing to say. A part NDH deliberately does not
    /// model, or not a part at all.
    /// </summary>
    void Skipped(string navn, string why);
}

/// <summary>
/// WHAT ONE LEGACY BLOCK BECOMES IN NDH.
///
/// Authority: NorsynDrawingTools
/// <c>docs/shared-understanding/legacy-fjv-import.md</c>
/// &lt;every-block-settled-2026-09-21&gt;.
///
/// Four verdicts, and the last three are NOT one answer. The distinction is the
/// owner's and it is about what the DRAFTER is told: a part NDH has not built
/// yet is a deficit and must be loud, because a drawing that quietly lost a
/// valve is worse than one that says it could not place it; a part NDH
/// deliberately does not model is the model agreeing with itself, and a mark
/// there would ask the drafter to fix something that is not broken.
///
/// Each verdict tells its own audience. There is no <c>Kind</c> to read and
/// nothing switches on one.
/// </summary>
internal abstract record LegacyVerdict
{
    internal abstract void Tell(string navn, ILegacyVerdictAudience audience);
}

/// <summary>Translate to this Produkt.</summary>
internal sealed record PartIsProdukt(string Produkt) : LegacyVerdict
{
    internal override void Tell(string navn, ILegacyVerdictAudience audience) =>
        audience.Translates(navn, Produkt);
}

/// <summary>
/// A real part with no NDH counterpart YET. The one verdict that is a deficit:
/// it is marked in the drawing and it is reported.
///
/// This bucket is the one that empties as the catalogue grows, which is why it
/// is kept apart from the two below - they never do.
/// </summary>
internal sealed record PartNotImplemented(string Note, string Reason) : LegacyVerdict
{
    internal override void Tell(string navn, ILegacyVerdictAudience audience) =>
        audience.Missing(navn, Note, Reason);
}

/// <summary>
/// A part NDH deliberately does not model. Skipped in SILENCE - never marked,
/// never reported as a problem.
/// </summary>
internal sealed record PartNotNeeded(string Why) : LegacyVerdict
{
    internal override void Tell(string navn, ILegacyVerdictAudience audience) =>
        audience.Skipped(navn, Why);
}

/// <summary>
/// Not a part at all - a weld mark, a label. Skipped in silence too, and kept
/// distinct from <see cref="PartNotNeeded"/> because the two are different
/// facts about the world even where they lead to the same act.
/// </summary>
internal sealed record PartNotAFitting(string Why) : LegacyVerdict
{
    internal override void Tell(string navn, ILegacyVerdictAudience audience) =>
        audience.Skipped(navn, Why);
}
