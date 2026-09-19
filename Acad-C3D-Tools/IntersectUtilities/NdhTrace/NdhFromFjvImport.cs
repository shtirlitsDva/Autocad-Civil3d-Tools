using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// The outcome of building one new pipeline. Where a refusal names a place,
/// VertexIndex or SegmentIndex (segment k runs from vertex k to k+1) says
/// where; -1 when it names none.
/// </summary>
internal readonly record struct NdhBuildOutcome(
    bool Success, string Status, string Handle, int VertexIndex, int SegmentIndex, string Detail);

/// <summary>Creates one new pipeline in the working drawing from a route.</summary>
internal interface INdhPipelineBuilder
{
    NdhBuildOutcome Build(string name, NdhRoute route);
}

/// <summary>What NDHFROMFJV did, section by section, for the command's (Danish) report.</summary>
internal sealed class NdhImportReport
{
    public int LegacyPipelineCount { get; set; }
    /// <summary>Set when the drafter stopped the import in a dialog; nothing was written.</summary>
    public string? Cancelled { get; set; }
    public List<string> Created { get; } = new List<string>();
    /// <summary>Per merged pipeline, the legacy pipelines it absorbed.</summary>
    public List<string> Merged { get; } = new List<string>();
    /// <summary>Branches connected to their mains, with the Produkt pinned.</summary>
    public List<string> Connected { get; } = new List<string>();
    /// <summary>Every place the import changed the legacy geometry: routes and squared branches.</summary>
    public List<string> Adjusted { get; } = new List<string>();
    /// <summary>What the import left out, and why: untraced pipelines, untranslatable parts, stik.</summary>
    public List<string> Skipped { get; } = new List<string>();
    /// <summary>What NDH refused: pipelines and connections.</summary>
    public List<string> Refused { get; } = new List<string>();
    public List<string> ProducerReport { get; } = new List<string>();
    public List<string> SeriesReport { get; } = new List<string>();
}

/// <summary>Everything the import talks to, so each piece can be replaced.</summary>
internal sealed record NdhImportServices(
    INdhPipelineBuilder Builder,
    INdhPartStraight Straight,
    INdhConnector Connector,
    INdhDrawingSettings Settings,
    IImportDialogs Dialogs,
    IImportMarkers Markers);

/// <summary>
/// NDHFROMFJV (#319): translates a legacy FJV drawing word for word into NDH
/// pipelines and their connections, in the order the spec fixes:
/// 1. read the legacy drawing;
/// 2. infer the drawing settings, asking where the legacy drawing is ambiguous;
/// 3. set the drawing's producer and series matrix;
/// 4. merge end-to-end chains;
/// 5. build the pipelines;
/// 6. connect the branches;
/// 7. place the markers;
/// 8. report (the command prints it).
///
/// Everything runs inside the one NDHFROMFJV command, so one UNDO takes the
/// whole import back: the NDH exports commit undoable transactions of the
/// command, and the markers are written in the command too.
/// </summary>
internal static class NdhFromFjvImport
{
    public static NdhImportReport Run(string fjvPath, NdhImportServices services)
    {
        NdhImportReport report = new NdhImportReport();

        //1.
        using LegacyDrawing legacy = LegacyDrawingReader.Read(fjvPath);
        report.LegacyPipelineCount = legacy.Traces.Traces.Count + legacy.Traces.Skipped.Count;
        foreach (string s in legacy.Traces.Skipped) report.Skipped.Add($"Rørledning ikke sporet: {s}");

        //2. + 3.
        if (!DrawingSettingsStep.Apply(legacy.Settings, services.Settings, services.Dialogs, report))
            return report;

        //4.
        using MergedTraces merged = LegacyTraceMerger.Merge(legacy.Traces.Traces, legacy.Joins, legacy.DepthOf);
        report.Merged.AddRange(merged.Merged);
        report.Skipped.AddRange(merged.Notes);

        //5.
        Dictionary<string, BuiltPipeline> built = Build(legacy, merged, services, report);

        //6.
        List<ImportMarker> markers = BranchConnectionStep.Run(legacy, merged, built, services.Connector, report);

        //7.
        services.Markers.Place(markers);

        return report;
    }

    /// <summary>
    /// Routes and builds every pipeline, without any transaction of ours open:
    /// the builder opens the working drawing's model space itself. Each route
    /// is told where the legacy branches sit on it, so it stays straight across
    /// them: a legacy junction stands on a straight (live run 2026-09-19, F2).
    /// </summary>
    private static Dictionary<string, BuiltPipeline> Build(
        LegacyDrawing legacy, MergedTraces merged, NdhImportServices services, NdhImportReport report)
    {
        ILookup<string, NdhJunctionSeat> seats = legacy.Branches.ToLookup(
            b => merged.NameOf(b.MainName),
            b => new NdhJunctionSeat(b.Site, b.MainPorts),
            StringComparer.Ordinal);

        Dictionary<string, BuiltPipeline> built = new Dictionary<string, BuiltPipeline>(StringComparer.Ordinal);
        foreach (LegacyPipelineTrace t in merged.Traces)
        {
            NdhRoute route;
            try
            {
                route = NdhRouteBuilder.Build(t, services.Straight, seats[t.Name].ToList());
            }
            catch (Exception ex)
            {
                report.Skipped.Add($"Rørledning ikke sporet: {t.Name}: {ex.Message}");
                continue;
            }

            NdhBuildOutcome outcome = services.Builder.Build(t.Name, route);
            if (!outcome.Success)
            {
                string at =
                    outcome.VertexIndex >= 0 ? $" (vertex {outcome.VertexIndex})" :
                    outcome.SegmentIndex >= 0 ? $" (segment {outcome.SegmentIndex})" : "";
                report.Refused.Add($"{t.Name}: {outcome.Status}{at} - {outcome.Detail}");
                continue;
            }

            built[t.Name] = new BuiltPipeline(outcome.Handle, route);
            report.Created.Add(t.Name);
            foreach (string note in route.Adjustments) report.Adjusted.Add($"{t.Name}: {note}");
        }
        return built;
    }
}
