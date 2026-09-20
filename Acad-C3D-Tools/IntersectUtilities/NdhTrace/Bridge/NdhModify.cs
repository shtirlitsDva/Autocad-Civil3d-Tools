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
}
