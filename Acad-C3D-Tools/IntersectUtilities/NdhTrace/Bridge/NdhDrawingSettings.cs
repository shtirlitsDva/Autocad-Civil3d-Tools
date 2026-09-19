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
}
