namespace IntersectUtilities.NdhTrace;

/// <summary>
/// WHAT A CENSUS DOES WITH ONE BLOCK. The two situations answer differently and
/// neither has to be asked what it is: a block whose situation the rule sheet
/// decides is COUNTED toward the drawing's policy, and a block whose part is
/// decided somewhere else is passed over with its reason.
///
/// Passing over is not a complaint. The part register already tells the drafter
/// about a block NDH cannot place; a census has nothing to add to that, and a
/// second message about a Y-rør that translated perfectly well would be noise.
/// </summary>
internal interface ISituationAudience
{
    /// <summary>
    /// This block stands in a situation the sheet decides. Count it.
    /// <paramref name="cause"/> is the KIND of component it becomes, which an
    /// override has to name beside the vertex - carried here rather than worked
    /// out later, so nothing has to translate a situation into a cause.
    /// </summary>
    void Counts(string situationToken, NdhCause cause);

    /// <summary>Somebody else decides this block's part. Why, for the report's detail.</summary>
    void PassedOver(string why);
}

/// <summary>Which NDH situation one legacy block stands in.</summary>
internal abstract record LegacySituation
{
    internal abstract void Tell(ISituationAudience audience);
}

/// <summary>
/// The drawing's fitting rule sheet decides this block's part, in this
/// situation - so what the old drawing drew here IS policy, and is counted.
/// </summary>
internal sealed record SheetDecides(string SituationToken, NdhCause Cause) : LegacySituation
{
    internal override void Tell(ISituationAudience audience) =>
        audience.Counts(SituationToken, Cause);
}

/// <summary>
/// The sheet does not decide this block's part, and the reason is stated rather
/// than implied by absence. Three different reasons wear this one type because
/// the census does the same thing with all of them - passes over, and says why.
/// </summary>
internal sealed record DecidedElsewhere(string Why) : LegacySituation
{
    internal override void Tell(ISituationAudience audience) => audience.PassedOver(Why);
}
