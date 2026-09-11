using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;

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

internal sealed class NdhImportReport
{
    public int LegacyPipelineCount { get; set; }
    public List<string> Created { get; } = new List<string>();
    /// <summary>Pipelines the builder refused, with its reason.</summary>
    public List<string> Refused { get; } = new List<string>();
    /// <summary>Legacy pipelines that produced no route, with the reason.</summary>
    public List<string> Skipped { get; } = new List<string>();
    /// <summary>Per created pipeline, every place its route deviates from the trace.</summary>
    public List<string> Adjusted { get; } = new List<string>();
}

/// <summary>
/// Traces every legacy FJV pipeline of a source drawing and creates a new
/// pipeline for each in the working drawing.
/// </summary>
internal static class NdhFromFjvImport
{
    public static NdhImportReport Run(string fjvPath, INdhPipelineBuilder builder)
    {
        NdhImportReport report = new NdhImportReport();
        List<(string Name, NdhRoute Route)> routes = new List<(string, NdhRoute)>();

        using (Database fjvDb = new Database(false, true))
        {
            fjvDb.ReadDwgFile(fjvPath, FileOpenMode.OpenForReadAndAllShare, true, "");
            using Transaction tx = fjvDb.TransactionManager.StartTransaction();
            using LegacyTraceResult traces = FjvLegacyPipelineReader.Read(fjvDb, tx);

            report.LegacyPipelineCount = traces.Traces.Count + traces.Skipped.Count;
            report.Skipped.AddRange(traces.Skipped);

            foreach (LegacyPipelineTrace t in traces.Traces)
            {
                try
                {
                    routes.Add((t.Name, NdhRouteBuilder.Build(t)));
                }
                catch (Exception ex)
                {
                    report.Skipped.Add($"{t.Name}: {ex.Message}");
                }
            }

            //The source is only read; nothing in it may be kept.
            tx.Abort();
        }

        //Built only after the source is closed and without any transaction of
        //ours: the builder opens the working drawing's model space itself.
        foreach ((string name, NdhRoute route) in routes)
        {
            NdhBuildOutcome outcome = builder.Build(name, route);
            if (!outcome.Success)
            {
                string at =
                    outcome.VertexIndex >= 0 ? $" (vertex {outcome.VertexIndex})" :
                    outcome.SegmentIndex >= 0 ? $" (segment {outcome.SegmentIndex})" : "";
                report.Refused.Add($"{name}: {outcome.Status}{at} - {outcome.Detail}");
                continue;
            }

            report.Created.Add(name);
            foreach (string note in route.Adjustments) report.Adjusted.Add($"{name}: {note}");
        }

        return report;
    }
}
