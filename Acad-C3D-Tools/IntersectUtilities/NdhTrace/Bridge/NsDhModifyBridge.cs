using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// NsDh_ModifyPipeline, mirrored field for field from NsDhPipelineBridge.h
/// (sizes and offsets in the comments are the header's static_asserts).
/// </summary>
internal sealed class NsDhModifyBridge : INdhPipelineModifier
{
    //kNsDhEdit* - which fields of an edit row are read.
    private const int EditSlideVertex = 0;
    private const int EditFittingSelection = 7;

    //kNsDhCause* / kNsDhRun* are NdhCause and NdhRun - the enums hold the wire
    //values themselves, so this file declares no constants for them and casts
    //straight through. They are the other two thirds of a component's name: one
    //route vertex causes SEVERAL components - an elbow and a reducer can share
    //a boundary vertex, and a bonded pair's Frem and Retur each carry their own
    //at that one vertex - so an override that states only the vertex names none
    //of them. Before modify version 3 the dbx filled these in as Elbow and
    //Twin, and an override on anything else was a silent no-op returning Ok.

    //sizeof 208
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PipelineEdit
    {
        public int Kind;           //off 0
        public int Vertex;         //off 4
        public int End;            //off 8
        public int Present;        //off 12
        public int Custom;         //off 16
        public int CauseKind;      //off 20
        public int RunRole;        //off 24
        public double AlongM;      //off 32
        public double Dx;          //off 40
        public double Dy;          //off 48
        public double ByM;         //off 56
        public double LegMm;       //off 64
        public ulong CauseHandle;  //off 72
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Name; //off 80
    }

    //sizeof 2096
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ModifyResult
    {
        public int Status;          //off 0
        public int RefusedRow;      //off 4
        public int RunsTouched;     //off 8
        public int IssueCount;      //off 12
        public int SubjectKnown;    //off 16
        public int Reserved;        //off 20
        public double LargestMoveM; //off 24
        public double SubjectX;     //off 32
        public double SubjectY;     //off 40
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string Detail; //off 48
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int ModifyPipelineFn(
        [MarshalAs(UnmanagedType.LPWStr)] string pipelineHandle,
        [In] PipelineEdit[] edits,
        int count,
        out ModifyResult result);

    static NsDhModifyBridge()
    {
        NsDhModule.RequireLayout<PipelineEdit>(208);
        NsDhModule.RequireLayout<ModifyResult>(2096);
    }

    public NdhModifyOutcome SlideVertices(
        string pipelineHandle, IReadOnlyList<(int Vertex, double AlongM)> slides)
    {
        //Every field an arm does not own is left at its default: the header
        //states that a field the kind does not read is not read. A route arm
        //names no component, so CauseKind and RunRole are among them.
        return Apply(pipelineHandle, slides, nameof(slides), s => new PipelineEdit
        {
            Kind = EditSlideVertex,
            Vertex = s.Vertex,
            AlongM = s.AlongM,
            Name = "",
        });
    }

    public NdhModifyOutcome SetFittingChoices(
        string pipelineHandle, IReadOnlyList<NdhFittingOverride> choices)
    {
        //The choice spells its own wire state; nothing here asks what it is.
        return Apply(pipelineHandle, choices, nameof(choices), c => new PipelineEdit
        {
            Kind = EditFittingSelection,
            CauseHandle = c.Component.CauseHandle,
            CauseKind = (int)c.Component.Cause,
            RunRole = (int)c.Component.Run,
            Present = c.Choice.Present,
            Name = c.Choice.Name,
        });
    }

    /// <summary>
    /// ONE LIST IS ONE EDIT, whatever the arms are: the marshalling, the call
    /// and the reading of the result are the same for every arm, and only the
    /// row each one writes differs.
    /// </summary>
    private static NdhModifyOutcome Apply<T>(
        string pipelineHandle, IReadOnlyList<T> arms, string argName, Func<T, PipelineEdit> row)
    {
        if (arms.Count == 0)
            throw new ArgumentException("An edit list with no arms.", argName);

        ModifyPipelineFn modify = NsDhModule.Resolve<ModifyPipelineFn>(
            NsDhSurface.PipelineModify, "NsDh_ModifyPipeline");

        PipelineEdit[] edits = new PipelineEdit[arms.Count];
        for (int i = 0; i < arms.Count; i++) edits[i] = row(arms[i]);

        (NdhModifyStatus named, string detail) = NsDhModule.Named(
            modify(pipelineHandle, edits, edits.Length, out ModifyResult r),
            r.Detail, NdhModifyStatus.Failed);

        return new NdhModifyOutcome(
            named, r.RefusedRow, r.RunsTouched, r.IssueCount, r.SubjectKnown != 0,
            r.LargestMoveM, r.SubjectX, r.SubjectY, detail);
    }
}
