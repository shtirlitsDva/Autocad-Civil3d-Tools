using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace IntersectUtilities.MPE.AutoAmkB;

/// <summary>Where a hit sits on its alignment: at one station (a crossing) or along a stretch.</summary>
internal abstract record HitExtent
{
    private HitExtent() { }

    public abstract TOut Match<TOut>(Func<double, TOut> point, Func<double, double, TOut> stretch);

    public static HitExtent Point(double station) => new AtPoint(station);

    public static HitExtent Stretch(double from, double to) => new Along(from, to);

    public double From => Match(station => station, (from, _) => from);

    public double To => Match(station => station, (_, to) => to);

    public double Length => Match(_ => 0.0, (from, to) => to - from);

    internal sealed record AtPoint(double Station) : HitExtent
    {
        public override TOut Match<TOut>(Func<double, TOut> point, Func<double, double, TOut> stretch) => point(Station);
    }

    internal sealed record Along(double FromStation, double ToStation) : HitExtent
    {
        public override TOut Match<TOut>(Func<double, TOut> point, Func<double, double, TOut> stretch) =>
            stretch(FromStation, ToStation);
    }
}

/// <summary>The raw data behind a hit, written to the source-data columns beside the paste block.</summary>
internal sealed record HitTrace(string Detail, int Count, string Owner, string Source, string Handle, double X, double Y);

internal sealed record Hit(
    HitKind Kind,
    string Alignment,
    HitExtent Extent,
    IReadOnlyDictionary<string, string> Values,
    HitTrace Trace);

internal sealed record ValveInfo(
    string BlockName,
    string Handle,
    string Designation,
    string Dn,
    string System,
    string ElementType,
    double X,
    double Y);

/// <summary>One valve placement: a single valve, or the supply/return pair of a bonded system.</summary>
internal sealed record ValveLocation(Option<string> Alignment, Option<double> Station, IReadOnlyList<ValveInfo> Valves);

internal sealed record DataFileInfo(string Role, string Path, Option<DateTime> Modified);

/// <summary>One step of a run: what is being done, and how far the run has come (0 to 1).</summary>
internal readonly record struct AmkProgress(string Text, double Done);

internal sealed record AmkReport(
    string ProjectId,
    string EtapeId,
    string DrawingPath,
    DateTime RunTime,
    string Configuration,
    LoadedRules Rules,
    IReadOnlyList<Hit> Hits,
    IReadOnlyList<ValveLocation> Valves,
    IReadOnlyList<AlignmentTrace> Traces,
    IReadOnlyList<DataFileInfo> DataFiles,
    IReadOnlyList<string> NotEvaluated,
    IReadOnlyList<string> Warnings);

/// <summary>The station notation the AMK files already use: alignment, colon, whole metres rounded down.</summary>
internal static class StationText
{
    public static string Point(string alignment, double station) => $"{alignment}:{Metres(station)}";

    public static string Stretch(string alignment, double from, double to) => $"{alignment}:{Metres(from)}-{Metres(to)}";

    public static string Of(string alignment, HitExtent extent) =>
        extent.Match(station => Point(alignment, station), (from, to) => Stretch(alignment, from, to));

    private static string Metres(double station) =>
        ((long)Math.Floor(station + 1e-6)).ToString("000", CultureInfo.InvariantCulture);
}

/// <summary>Fills {placeholders} in the texts from AutoAmkB.csv. An unknown placeholder is left as written.</summary>
internal static class AmkTemplate
{
    private static readonly Regex Placeholder = new Regex(@"\{([^{}]+)\}", RegexOptions.Compiled);

    public static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        Placeholder.Replace(template, match => values.Lookup(match.Groups[1].Value).OrElse(match.Value));

    /// <summary>Distinct, non-empty texts joined for a merged row, e.g. "10/50".</summary>
    public static string JoinDistinct(IEnumerable<string> texts, string separator) =>
        string.Join(separator, texts.Where(text => text.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
}
