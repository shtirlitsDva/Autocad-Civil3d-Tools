using IntersectUtilities.PlanDetailing;

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
    NdhBuildStatus Status, string Handle, int VertexIndex, int SegmentIndex, string Detail,
    IReadOnlyList<ulong> VertexCauses)
{
    public bool Success => Status == NdhBuildStatus.Ok;

    /// <summary>
    /// THE DURABLE CAUSE OF THE VERTEX THE CALLER ASKED FOR AT
    /// <paramref name="authoredIndex"/> - the id an override arm names, so that
    /// what this pipeline drew can be argued with afterwards.
    ///
    /// IN THE CALLER'S OWN NUMBERING. A boundary asks for parts and each part
    /// takes a vertex of its own, so the pipeline that stands carries more
    /// vertices than were asked for; those belong to parts nobody authored and
    /// are not named here.
    ///
    /// Zero for a build that refused, and for an index outside the route. Zero
    /// is no vertex, and an override written on it would be refused by name.
    /// </summary>
    public ulong CauseAt(int authoredIndex) =>
        authoredIndex >= 0 && authoredIndex < VertexCauses.Count ? VertexCauses[authoredIndex] : 0UL;
}

/// <summary>NsDh_BuildPipeline status codes (kNsDhPipelineBuild*); NsDh_ChangeStraight and NsDh_ElbowStraight answer in them too.</summary>
internal enum NdhBuildStatus
{
    Ok = 0,
    /// <summary>The call is malformed: a caller bug, not a statement about the pipeline.</summary>
    BadArgs = -1,
    /// <summary>The request breaks a rule of its own (too few vertices, an unknown token, ...).</summary>
    InvalidRequest = -2,
    /// <summary>The solver refused the route.</summary>
    RouteDoesNotSolve = -3,
    /// <summary>The name is empty, or another pipeline holds it.</summary>
    NameUnavailable = -4,
    /// <summary>A boundary stands on a vertex that already carries an elbow.</summary>
    TwoFittingsAtOneVertex = -5,
    /// <summary>Anything else; NDHTRACE carries the reason.</summary>
    BuildFailed = -6,
}

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
    /// <summary>How many markers were placed on the marker layer.</summary>
    public int MarkersPlaced { get; set; }
    /// <summary>
    /// Every pipeline the import built, in build order, with the handle NDH
    /// gave it: what the caller needs to ask NDH about them afterwards.
    /// </summary>
    public List<(string Name, string Handle)> Built { get; } = new List<(string, string)>();
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
///
/// The report belongs to the caller and is filled as the import goes: when a
/// step throws after the first write, what was already built and connected is
/// still in it, for the command to print (review of #319, I2).
/// </summary>
internal static class NdhFromFjvImport
{
    public static void Run(string fjvPath, NdhImportServices services, NdhImportReport report)
    {
        //1.
        using LegacyDrawing legacy = LegacyDrawingReader.Read(fjvPath);
        report.LegacyPipelineCount = legacy.Traces.Traces.Count + legacy.Traces.Skipped.Count;
        foreach (string s in legacy.Traces.Skipped) report.Skipped.Add($"Rørledning ikke sporet: {s}");
        List<ImportMarker> transitionMarkers = UnjoinedTransitions(legacy, report);

        //2. + 3.
        if (!DrawingSettingsStep.Apply(legacy.Settings, services.Settings, services.Dialogs, report))
            return;

        //4.
        using MergedTraces merged = LegacyTraceMerger.Merge(legacy.Traces.Traces, legacy.Joins, legacy.DepthOf);
        report.Merged.AddRange(merged.Merged);
        report.Skipped.AddRange(merged.Notes);

        //5.
        Dictionary<string, BuiltPipeline> built = Build(legacy, merged, services, report);

        //6.
        List<ImportMarker> markers = BranchConnectionStep.Run(legacy, merged, built, services.Connector, report);
        markers.AddRange(transitionMarkers);

        //7.
        report.MarkersPlaced = services.Markers.Place(markers);
    }

    /// <summary>
    /// NDH's Issue Ledger for the pipelines the import built, one sentence per
    /// complaint, in build order. A pipeline that cannot be read is said so
    /// rather than passed over: a silent ledger and an unreadable one must not
    /// look alike.
    ///
    /// Safe to read as soon as the pipelines are built: a connect re-derives
    /// BOTH sides, so a ledger is settled when NsDh_ConnectBranch returns. That
    /// was not always true - before NDH's step (5) landed (2026-09-20) pipeline
    /// 059 read 0 complaints inside the command and 1 the moment it was over,
    /// and a Regen inside the command did not settle it either.
    /// </summary>
    public static List<string> Complaints(
        IReadOnlyList<(string Name, string Handle)> built, INdhPipelineIssues issues)
    {
        List<string> said = new List<string>();
        foreach ((string name, string handle) in built)
        {
            IReadOnlyList<NdhIssueRow> rows;
            try
            {
                rows = issues.Read(handle);
            }
            catch (Exception ex)
            {
                said.Add($"{name}: NDH's bemærkninger kunne ikke læses ({ex.Message}).");
                continue;
            }

            foreach (NdhIssueRow r in rows)
            {
                string where =
                    r.Place == NdhIssuePlace.OnPipeline ? "" :
                    r.Run.Length > 0 ? $" [{r.Run} {r.Station:F1} m]" : $" [{r.Station:F1} m]";
                said.Add($"{name}{where} {r.CodeName}: {r.Detail}");
            }
        }
        return said;
    }

    /// <summary>
    /// A construction change the legacy pipeline's Centreline does not run
    /// through is not in the new pipeline: NDH lays a change where the pipe's
    /// identity changes along ONE pipeline, and this one stands where the
    /// pipeline stops, or between two sides that never met. The import
    /// translates and never designs, so it does not invent the change; it
    /// reports it and marks where the legacy drawing has it, as it does every
    /// other part it could not carry across (decided from the drafter's seat).
    /// </summary>
    private static List<ImportMarker> UnjoinedTransitions(LegacyDrawing legacy, NdhImportReport report)
    {
        List<ImportMarker> markers = new List<ImportMarker>();
        foreach ((string pipeline, UnjoinedTransition t) in legacy.Traces.UnjoinedTransitions)
        {
            report.Skipped.Add($"{pipeline}: konstruktionsskift {t.Part} er ikke overført ({t.Reason}).");
            markers.Add(new NotConnectedMarker(t.At,
                $"NDHFROMFJV: legacy transition {t.Part} on '{pipeline}' not carried across: {t.Reason}."));
        }
        return markers;
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
            report.Built.Add((t.Name, outcome.Handle));
            foreach (string note in route.Adjustments) report.Adjusted.Add($"{t.Name}: {note}");
        }
        return built;
    }
}
