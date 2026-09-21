using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// One cell of the drawing's series matrix: from <paramref name="Dn"/> (DN for
/// steel, outer diameter in mm for the others - as an identity boundary says
/// it) of the system and type named by their catalogue tokens, the pipe is
/// series <paramref name="Series"/> (1, 2 or 3).
/// </summary>
internal readonly record struct NdhSeriesCell(string SystemToken, string TypeToken, int Dn, int Series);

/// <summary>The drawing-settings exports' status codes (kNsDhSettings*).</summary>
internal enum NdhSettingsStatus
{
    Ok = 0,
    /// <summary>A null pointer, a negative count.</summary>
    BadArgs = -1,
    /// <summary>An unknown token, series not 1..3, dn &lt;= 0.</summary>
    InvalidRequest = -2,
    /// <summary>The catalogue carries no such series at that size.</summary>
    Unservable = -3,
    /// <summary>A linked sheet, the runtime; NDHTRACE carries it.</summary>
    Failed = -4,
    /// <summary>A read that did not fit; ask again.</summary>
    BufferTooSmall = -5,
}

/// <summary>
/// What NsDh_SetProducer / NsDh_SetSeriesMatrix did. <paramref name="CellIndex"/>
/// names the offending cell, -1 when the refusal names none.
/// </summary>
internal readonly record struct NdhSettingsOutcome(NdhSettingsStatus Status, int CellIndex, string Detail)
{
    public bool Success => Status == NdhSettingsStatus.Ok;
}

/// <summary>The working drawing's NDH settings the import sets from the legacy drawing.</summary>
internal interface INdhDrawingSettings
{
    /// <summary>The drawing's current producer token (Logstor | Isoplus).</summary>
    string ReadProducer();

    NdhSettingsOutcome SetProducer(string producerToken);

    /// <summary>The cells the drawing has assigned (unassigned sizes take the catalogue's seed).</summary>
    IReadOnlyList<NdhSeriesCell> ReadSeriesMatrix();

    /// <summary>
    /// Every system named in <paramref name="cells"/> has its grid REPLACED by
    /// the cells given for it, in one undoable step (contract D6).
    /// </summary>
    NdhSettingsOutcome SetSeriesMatrix(IReadOnlyList<NdhSeriesCell> cells);

    /// <summary>
    /// The whole sheet the drawing holds, in its own order, every system. A row
    /// whose situation this build does not publish comes back AS FILED: it is
    /// inert, not absent, and a fitter replacing a system's policy has to see
    /// which row it is replacing.
    /// </summary>
    IReadOnlyList<NdhFittingRule> ReadFittingRules();

    /// <summary>
    /// The rows are PREPENDED to the sheet, IN THE ORDER GIVEN, in one undoable
    /// step. NOTHING IS DELETED: first match wins, so a row handed in here
    /// beats the seed row it competes with, and the seed rows stay behind it
    /// answering every situation this caller said nothing about.
    ///
    /// This replaced a call that wiped every row of every system it named. A
    /// fitter reads an OLD DRAWING, and an old drawing only speaks for the
    /// situations it happens to contain - so wiping a system's policy left
    /// every other situation in that system with no rule at all, and the import
    /// filled with complaints about corners the defaults had always covered.
    ///
    /// There is still no edit-by-index and no move: an index into a sheet the
    /// caller did not author is a race it cannot see.
    /// </summary>
    NdhSettingsOutcome AddFittingRules(IReadOnlyList<NdhFittingRule> rows);

    /// <summary>
    /// Copy the settings profile the drawing is ON into a new one and switch to
    /// it, so every setting written afterwards lands on the COPY.
    /// <paramref name="wantedName"/> is a wish - a name already taken is
    /// uniquified, never replaced - and the name it landed on comes back in the
    /// outcome's detail.
    ///
    /// An import states a whole drawing's policy. Doing that to the profile the
    /// drafter set up would overwrite work nobody asked us to touch; on a copy,
    /// they switch back and their drawing is as they left it.
    /// </summary>
    NdhSettingsOutcome UseSettingsProfileCopy(string wantedName);
}
