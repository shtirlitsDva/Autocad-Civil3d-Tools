using IntersectUtilities.UtilsCommon.Enums;

using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>A pipe size the legacy drawing draws in more than one series.</summary>
internal sealed record AmbiguousSize(SeriesKey Key, IReadOnlyDictionary<PipeSeriesEnum, SeriesTally> Seen);

/// <summary>
/// Where the legacy drawing is ambiguous the drafter rules the wrong readings
/// out - in real dialogs, never command-line keywords. Null means the drafter
/// cancelled, and the import stops before it writes anything.
/// </summary>
internal interface IImportDialogs
{
    /// <summary>One producer token from the ones the legacy drawing names, with the parts naming each.</summary>
    string? ChooseProducer(IReadOnlyDictionary<string, SortedDictionary<string, int>> named);

    /// <summary>For each ambiguous size, the one series it is.</summary>
    IReadOnlyDictionary<SeriesKey, PipeSeriesEnum>? SettleSeries(IReadOnlyList<AmbiguousSize> sizes);
}
