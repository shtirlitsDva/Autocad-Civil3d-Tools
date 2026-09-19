using System;
using System.Runtime.InteropServices;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Builds pipelines through the NDH district-heating module's flat C export
/// NsDh_BuildPipeline, and asks it how much straight a change's and an elbow's
/// parts take through NsDh_ChangeStraight and NsDh_ElbowStraight. The module is
/// reached through <see cref="NsDhModule"/>.
/// </summary>
internal sealed class NsDhPipelineBridge : INdhPipelineBuilder, INdhPartStraight
{
    private const int StatusOk = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct RouteVertex
    {
        public double X;
        public double Y;
        public double BendRadius;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct IdentityBoundary
    {
        public int VertexIndex;
        public int Dn;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string System;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string Type;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BuildResult
    {
        public int Status;
        public int VertexIndex;
        public int SegmentIndex;
        public int Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string PipelineHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Detail;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PipeIdentity
    {
        public int Dn;
        public int Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string System;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string Type;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ChangeStraightResult
    {
        public int Status;
        public int Reserved;
        public double BackM;
        public double ForwardM;
        public double MinimumPipeM;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Detail;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int ChangeStraightFn(
        in PipeIdentity before, in PipeIdentity after, out ChangeStraightResult result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int ElbowStraightFn(
        in PipeIdentity pipe, double turnDegrees, out ChangeStraightResult result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int BuildPipelineFn(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        [In] RouteVertex[] vertices,
        int vertexCount,
        [In] IdentityBoundary[] boundaries,
        int boundaryCount,
        out BuildResult result);

    public NdhBuildOutcome Build(string name, NdhRoute route)
    {
        BuildPipelineFn build = NsDhModule.Resolve<BuildPipelineFn>(NsDhSurface.Build, "NsDh_BuildPipeline");

        RouteVertex[] vertices = new RouteVertex[route.Vertices.Count];
        for (int i = 0; i < vertices.Length; i++)
        {
            NdhRouteVertex v = route.Vertices[i];
            vertices[i] = new RouteVertex { X = v.X, Y = v.Y, BendRadius = v.BendRadius };
        }

        IdentityBoundary[] boundaries = new IdentityBoundary[route.Boundaries.Count];
        for (int i = 0; i < boundaries.Length; i++)
        {
            NdhIdentityBoundary b = route.Boundaries[i];
            boundaries[i] = new IdentityBoundary
            {
                VertexIndex = b.VertexIndex,
                Dn = b.Dn,
                System = NsDhModule.SystemToken(b.System),
                Type = NsDhModule.TypeToken(b.Type),
            };
        }

        int status = build(
            name, vertices, vertices.Length, boundaries, boundaries.Length, out BuildResult r);

        return new NdhBuildOutcome(
            status == StatusOk,
            StatusName(status),
            r.PipelineHandle ?? "",
            r.VertexIndex,
            r.SegmentIndex,
            r.Detail ?? "");
    }

    /// <summary>
    /// The straight the parts of a change take, under the working drawing's own
    /// catalogue and settings. A refusal is the module's sentence: the importer
    /// cannot place a change without it.
    /// </summary>
    public PartStraight OfChange(LegacyIdentitySpan before, LegacyIdentitySpan after)
    {
        ChangeStraightFn ask = NsDhModule.Resolve<ChangeStraightFn>(
            NsDhSurface.ChangeStraight, "NsDh_ChangeStraight");

        int status = ask(IdentityOf(before), IdentityOf(after), out ChangeStraightResult r);
        return Answered(status, r,
            $"the change from {before.System} {before.Type} {before.Dn} to {after.System} {after.Type} {after.Dn}");
    }

    /// <summary>
    /// The legs of the elbow a pipe takes on a sharp corner, asked the same way.
    /// </summary>
    public PartStraight OfElbow(LegacyIdentitySpan pipe, double turnDegrees)
    {
        //The elbow is versioned with the change straight: one surface.
        ElbowStraightFn ask = NsDhModule.Resolve<ElbowStraightFn>(
            NsDhSurface.ChangeStraight, "NsDh_ElbowStraight");

        int status = ask(IdentityOf(pipe), turnDegrees, out ChangeStraightResult r);
        return Answered(status, r,
            $"the {turnDegrees:F1} degree elbow of {pipe.System} {pipe.Type} {pipe.Dn}");
    }

    private static PartStraight Answered(int status, ChangeStraightResult r, string what) =>
        status == StatusOk
            ? new PartStraight(r.BackM, r.ForwardM, r.MinimumPipeM)
            : throw new InvalidOperationException(
                $"The straight of {what} could not be asked ({StatusName(status)}): {r.Detail}");

    private static PipeIdentity IdentityOf(LegacyIdentitySpan s) => new PipeIdentity
    {
        Dn = s.Dn,
        System = NsDhModule.SystemToken(s.System),
        Type = NsDhModule.TypeToken(s.Type),
    };

    private static string StatusName(int status) => status switch
    {
        0 => "Ok",
        -1 => "BadArgs",
        -2 => "InvalidRequest",
        -3 => "RouteDoesNotSolve",
        -4 => "NameUnavailable",
        -5 => "TwoFittingsAtOneVertex",
        -6 => "BuildFailed",
        _ => $"Status{status}",
    };
}
