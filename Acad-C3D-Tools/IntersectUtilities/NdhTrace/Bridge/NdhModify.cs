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
        System.Collections.Generic.IReadOnlyList<(ulong CauseHandle, NdhFittingChoice Choice)> choices);
}

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
