namespace IntersectUtilities.NdhTrace;

/// <summary>NsDh_ModifyPipeline status codes (kNsDhModify*).</summary>
internal enum NdhModifyStatus
{
    /// <summary>The whole list was applied.</summary>
    Ok = 0,
    /// <summary>The call was malformed; it never reached the drawing.</summary>
    BadArgs = -1,
    NotAPipeline = -2,
    /// <summary>
    /// A row could not be FORMED against this pipeline - a vertex the route
    /// does not have, a corner asked to slide. Nothing was written.
    /// </summary>
    Refused = -3,
    /// <summary>Lawful, but it could not be laid or written. Nothing was written.</summary>
    Failed = -4,
}

/// <summary>
/// What NDH did with an edit list. The list is ONE edit: one transaction, one
/// undo, and the first refusal ends it with nothing written.
///
/// <paramref name="SubjectX"/>/<paramref name="SubjectY"/> is where the first
/// route arm's vertex actually ENDED UP, measured rather than assumed - a
/// caller that asked for twenty-two microns wants to know it happened, and "no
/// error" is not that. <paramref name="SubjectKnown"/> is false when the list
/// carried no route arm. <paramref name="IssueCount"/> is how many complaints
/// the pipeline carries AFTER the edit settled, so a caller knows whether to
/// read them again.
/// </summary>
internal readonly record struct NdhModifyOutcome(
    NdhModifyStatus Status,
    int RefusedRow,
    int RunsTouched,
    int IssueCount,
    bool SubjectKnown,
    double LargestMoveM,
    double SubjectX,
    double SubjectY,
    string Detail)
{
    public bool Success => Status == NdhModifyStatus.Ok;
}

/// <summary>
/// Changes a pipeline that already stands, from outside an interactive
/// gesture. Every arm is a value; where the route lands is the solver's.
/// </summary>
internal interface INdhPipelineModifier
{
    /// <summary>
    /// Slide route vertices along their runs, all of them as ONE edit: one
    /// gesture, one solve, one undo. Positive is toward the run's finish. A
    /// corner cannot slide and is refused by name.
    /// </summary>
    NdhModifyOutcome SlideVertices(
        string pipelineHandle, System.Collections.Generic.IReadOnlyList<(int Vertex, double AlongM)> slides);

    /// <summary>
    /// Write the fitting override on one or more components, all of them as
    /// ONE edit. <c>CauseHandle</c> is the CAUSE VERTEX the component stands
    /// on, as the durable id the Issue Ledger is written in.
    /// </summary>
    NdhModifyOutcome SetFittingChoices(
        string pipelineHandle,
        System.Collections.Generic.IReadOnlyList<NdhFittingOverride> choices);

    /// <summary>
    /// Give an elbow its own two leg lengths, all of them as ONE edit. The
    /// legs are named by the SIDE of the corner they are on, never by the
    /// neighbour they point at, so they survive a reattach.
    /// </summary>
    NdhModifyOutcome SetElbowLegs(
        string pipelineHandle,
        System.Collections.Generic.IReadOnlyList<NdhElbowLegs> legs);
}

/// <summary>
/// AN ELBOW'S TWO LEGS, in millimetres. Ben 1 (<paramref name="LoMm"/>) is the
/// leg toward the LOWER-station neighbour and Ben 2 (<paramref name="HiMm"/>)
/// toward the higher - the corner SIDE, which is what makes the name durable.
///
/// Writing them turns the elbow's Specialmål mode on, because that is what
/// makes per-leg lengths editable at all; a leg given 0 is handed back to the
/// catalogue.
/// </summary>
internal readonly record struct NdhElbowLegs(NdhComponent Component, double LoMm, double HiMm);

/// <summary>
/// WHAT KIND OF COMPONENT a vertex caused. The values ARE the wire's
/// kNsDhCause* (NsDhPipelineBridge.h) - this enum is that vocabulary named, not
/// a second one mapped onto it, so nothing has to translate between them.
///
/// A BRANCH is deliberately absent. A branch component is named by a cause, a
/// run AND an assembly member - which body, which reducer - so it cannot be
/// named through this door, and the dbx refuses it by name rather than
/// resolving it to something else.
/// </summary>
internal enum NdhCause
{
    Elbow = 0,
    Reducer = 1,
    ConstructionChange = 3,
    Arc = 4,
    VerticalElbow = 5,
    /// <summary>
    /// An authored valve. One valve vertex causes one valve per run - the twin
    /// run's, or the bonded pair's Frem AND Retur - so naming the Produkt of a
    /// bonded valve is two rows, exactly as a bonded reduction is.
    /// </summary>
    Valve = 6,
}

/// <summary>
/// WHICH RUN a component stands on. A TWIN pipeline has one run and it is
/// <see cref="Twin"/>. A BONDED pipeline has two, and one authored cause yields
/// a SEPARATE, independently overridable component on each - so overriding a
/// bonded reduction is TWO rows, Frem and Retur, not one.
/// </summary>
internal enum NdhRun
{
    Twin = 0,
    Frem = 1,
    Retur = 2,
}

/// <summary>
/// THE NAME OF ONE COMPONENT: the vertex that caused it, which KIND of
/// component it is, and which run it stands on. All three, because one route
/// vertex causes several - an elbow and a reducer can share a boundary vertex.
///
/// Naming only the vertex is not a partial answer, it is a WRONG one: the dbx
/// used to complete it as "the elbow of the twin run" and return Ok while the
/// component kept resolving through the rule sheet.
/// </summary>
internal readonly record struct NdhComponent(ulong CauseHandle, NdhCause Cause, NdhRun Run)
{
    /// <summary>The elbow at this vertex, on this run.</summary>
    public static NdhComponent Elbow(ulong causeHandle, NdhRun run) =>
        new(causeHandle, NdhCause.Elbow, run);

    /// <summary>The reducer at this boundary vertex, on this run.</summary>
    public static NdhComponent Reducer(ulong causeHandle, NdhRun run) =>
        new(causeHandle, NdhCause.Reducer, run);

    /// <summary>The transition between a bonded and a twin construction.</summary>
    public static NdhComponent ConstructionChange(ulong causeHandle, NdhRun run) =>
        new(causeHandle, NdhCause.ConstructionChange, run);

    /// <summary>The arc - a bend drawn as a curve rather than a corner.</summary>
    public static NdhComponent Arc(ulong causeHandle, NdhRun run) =>
        new(causeHandle, NdhCause.Arc, run);

    /// <summary>The elbow that turns out of plan - a vertical bend.</summary>
    public static NdhComponent VerticalElbow(ulong causeHandle, NdhRun run) =>
        new(causeHandle, NdhCause.VerticalElbow, run);

    /// <summary>The valve standing on this vertex, on this run.</summary>
    public static NdhComponent Valve(ulong causeHandle, NdhRun run) =>
        new(causeHandle, NdhCause.Valve, run);
}

/// <summary>One component, and the part it should use.</summary>
internal readonly record struct NdhFittingOverride(NdhComponent Component, NdhFittingChoice Choice);

/// <summary>
/// WHICH PART A COMPONENT USES, when the drawing's rule sheet does not answer
/// what this particular component is. THREE states, each its own type, because
/// "the sheet governs" and "no part at all" are different answers and a
/// nullable string would collapse them into one.
///
/// Each state carries its own wire spelling, so nothing anywhere asks a choice
/// what it is (kNsDhFitting* in NsDhPipelineBridge.h).
/// </summary>
internal abstract record NdhFittingChoice
{
    /// <summary>kNsDhFitting*: which of the three states this is.</summary>
    internal abstract int Present { get; }

    /// <summary>The Produkt name, where the state has one.</summary>
    internal virtual string Name => "";
}

/// <summary>Hand this component back to the rule sheet: the override is erased.</summary>
internal sealed record NdhFromRuleSheet : NdhFittingChoice
{
    internal override int Present => 0;  //kNsDhFittingErase
}

/// <summary>This component uses this Produkt, whatever the sheet says.</summary>
internal sealed record NdhFittingProdukt(string Produkt) : NdhFittingChoice
{
    internal override int Present => 1;  //kNsDhFittingProdukt
    internal override string Name => Produkt;
}

/// <summary>This component carries no part at all - the drafter's own answer.</summary>
internal sealed record NdhNoFitting : NdhFittingChoice
{
    internal override int Present => 2;  //kNsDhFittingNothing
}
