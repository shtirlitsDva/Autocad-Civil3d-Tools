using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IntersectUtilities.NdhTrace;

/// <summary>Where a complaint stands (kNsDhIssueOn*).</summary>
internal enum NdhIssuePlace
{
    /// <summary>On a run; <c>Station</c> is that run's chainage.</summary>
    OnRun = 0,
    /// <summary>Where a run could not be laid; <c>Station</c> is the centreline's.</summary>
    OnRoute = 1,
    /// <summary>On the pipeline as a whole; it stands at no point.</summary>
    OnPipeline = 2,
}

/// <summary>
/// One complaint on a built pipeline, as NDH's Issue Ledger holds it.
/// <paramref name="CodeName"/> is the planner's own word for it -
/// "FootprintOverlap", "SpoolTooShort", "RunNotDerivable", ... - which is what
/// a caller classifies by.
/// </summary>
internal readonly record struct NdhIssueRow(
    string CodeName, NdhIssuePlace Place, string Run, double Station, double X, double Y, string Detail);

/// <summary>Reads back what NDH thinks of a pipeline the import built.</summary>
internal interface INdhPipelineIssues
{
    /// <summary>Every complaint on the pipeline; throws when it cannot be read.</summary>
    IReadOnlyList<NdhIssueRow> Read(string pipelineHandle);
}

/// <summary>
/// NsDh_ReadPipelineIssues, mirrored field for field from NsDhPipelineBridge.h
/// (sizes and offsets in the comments are the header's static_asserts).
/// </summary>
internal sealed class NsDhIssuesBridge : INdhPipelineIssues
{
    //sizeof 2192
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PlanIssue
    {
        public int Code;        //off 0
        public int Place;       //off 4
        //HasMeasure is what says whether MeasureM means anything - NOT the
        //number, because zero is a real measured value (two footprints sharing
        //nothing is contact, which is legal).
        public int HasMeasure;  //off 8
        public int Reserved;    //off 12, explicit padding in the header
        public double MeasureM; //off 16
        public double Station;  //off 24
        public double X;        //off 32
        public double Y;        //off 40
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)] public string CodeName;  //off 48
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)] public string Run;        //off 128
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string Detail;  //off 144
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int ReadIssuesFn(
        [MarshalAs(UnmanagedType.LPWStr)] string pipelineHandle,
        [In, Out] PlanIssue[]? rows,
        int capacity,
        out int outCount);

    static NsDhIssuesBridge() => NsDhModule.RequireLayout<PlanIssue>(2192);

    public IReadOnlyList<NdhIssueRow> Read(string pipelineHandle)
    {
        ReadIssuesFn read = NsDhModule.Resolve<ReadIssuesFn>(
            NsDhSurface.PipelineRead, "NsDh_ReadPipelineIssues");

        //A capacity of 0 asks how many there are; most pipelines have none.
        (NdhPipelineReadStatus status, string said) = NsDhModule.Named(
            read(pipelineHandle, null, 0, out int count), null, NdhPipelineReadStatus.Failed);
        if (status != NdhPipelineReadStatus.Ok && status != NdhPipelineReadStatus.BufferTooSmall)
            throw new InvalidOperationException(
                $"The issues of pipeline {pipelineHandle} could not be read ({status}). {said}".TrimEnd());
        if (count == 0) return Array.Empty<NdhIssueRow>();

        PlanIssue[] rows = new PlanIssue[count];
        (status, said) = NsDhModule.Named(
            read(pipelineHandle, rows, rows.Length, out count), null, NdhPipelineReadStatus.Failed);
        if (status != NdhPipelineReadStatus.Ok)
            throw new InvalidOperationException(
                $"The issues of pipeline {pipelineHandle} could not be read ({status}). {said}".TrimEnd());

        List<NdhIssueRow> result = new List<NdhIssueRow>(count);
        for (int i = 0; i < count; i++)
        {
            PlanIssue r = rows[i];
            result.Add(new NdhIssueRow(
                r.CodeName ?? "", (NdhIssuePlace)r.Place, r.Run ?? "", r.Station, r.X, r.Y, r.Detail ?? ""));
        }
        return result;
    }
}
