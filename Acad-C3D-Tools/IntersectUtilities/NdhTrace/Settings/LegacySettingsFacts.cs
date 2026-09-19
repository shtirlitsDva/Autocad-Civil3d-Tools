using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// One pipe size of the drawing's series matrix: a system, Twin or the bonded
/// pair (NDH's Enkelt), and the size number (DN for steel, outer diameter for
/// the others - the number an identity boundary carries).
/// </summary>
internal readonly record struct SeriesKey(PipeSystemEnum System, bool Twin, int Dn) : IComparable<SeriesKey>
{
    public string SystemToken => NsDhModule.SystemToken(System);
    public string TypeToken => NsDhModule.TypeToken(Twin);
    public override string ToString() => $"{SystemToken} {TypeToken} {Dn}";

    public int CompareTo(SeriesKey other)
    {
        int c = System.CompareTo(other.System);
        if (c != 0) return c;
        c = Twin.CompareTo(other.Twin);
        return c != 0 ? c : Dn.CompareTo(other.Dn);
    }
}

/// <summary>How often, and over how much length, the legacy drawing draws a size in one series.</summary>
internal sealed class SeriesTally
{
    public int Pipes { get; set; }
    public double Length { get; set; }
    public override string ToString() => $"{Pipes} rør, {Length:F0} m";
}

/// <summary>
/// The drawing-wide facts the legacy drawing holds: which producers its parts
/// name, and which series each pipe size is drawn in.
/// </summary>
internal sealed class LegacySettingsFacts
{
    /// <summary>NDH producer token (Logstor | Isoplus) to the legacy parts naming it, with counts.</summary>
    public SortedDictionary<string, SortedDictionary<string, int>> Producers { get; } =
        new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);

    /// <summary>Per size, the series its legacy pipes are drawn in.</summary>
    public SortedDictionary<SeriesKey, SortedDictionary<PipeSeriesEnum, SeriesTally>> Series { get; } =
        new SortedDictionary<SeriesKey, SortedDictionary<PipeSeriesEnum, SeriesTally>>();

    /// <summary>Legacy parts naming a producer NDH does not set (not steel), with counts.</summary>
    public SortedDictionary<string, int> IgnoredProducerParts { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public void AddProducer(string token, string navn)
    {
        if (!Producers.TryGetValue(token, out SortedDictionary<string, int>? parts))
            Producers[token] = parts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        parts[navn] = parts.TryGetValue(navn, out int n) ? n + 1 : 1;
    }

    public void AddPipe(SeriesKey key, PipeSeriesEnum series, double length)
    {
        if (!Series.TryGetValue(key, out SortedDictionary<PipeSeriesEnum, SeriesTally>? tallies))
            Series[key] = tallies = new SortedDictionary<PipeSeriesEnum, SeriesTally>();
        if (!tallies.TryGetValue(series, out SeriesTally? tally))
            tallies[series] = tally = new SeriesTally();
        tally.Pipes++;
        tally.Length += length;
    }

    public static string Describe(IReadOnlyDictionary<string, int> parts) =>
        string.Join(", ", parts.Select(x => $"{x.Key} ×{x.Value}"));
}
