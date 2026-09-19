using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// One cell of the drawing's series matrix: from <paramref name="Dn"/> (DN for
/// steel, outer diameter in mm for the others - as an identity boundary says
/// it) of the system and type named by their catalogue tokens, the pipe is
/// series <paramref name="Series"/> (1, 2 or 3).
/// </summary>
internal readonly record struct NdhSeriesCell(string SystemToken, string TypeToken, int Dn, int Series);

/// <summary>NsDh_SetProducer / NsDh_SetSeriesMatrix status (kNsDhSettings*).</summary>
internal readonly record struct NdhSettingsOutcome(int Status, int CellIndex, string Detail)
{
    public bool Success => Status == 0;

    public string StatusName => Status switch
    {
        0 => "Ok",
        -1 => "BadArgs",
        -2 => "InvalidRequest",
        -3 => "Unservable",
        -4 => "Failed",
        -5 => "BufferTooSmall",
        _ => $"Status{Status}",
    };

    public bool Unservable => Status == -3;
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
