using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Builds pipelines through the NDH district-heating module's flat C export
/// NsDh_BuildPipeline, and asks it how much straight the things standing at a
/// vertex take through NsDh_VertexStraight. The module is reached through
/// <see cref="NsDhModule"/>.
/// </summary>
internal sealed class NsDhPipelineBridge : INdhPipelineBuilder, INdhPartStraight, INdhJunctionStraight
{
    //sizeof 24
    [StructLayout(LayoutKind.Sequential)]
    private struct RouteVertex
    {
        public double X;
        public double Y;
        public double BendRadius;
    }

    //sizeof 72
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct IdentityBoundary
    {
        public int VertexIndex;
        public int Dn;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string System;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string Type;
    }

    //sizeof 24. One authored valve on one of the caller's own vertices; the
    //staggers are each carrier's offset from that vertex along the route, in
    //metres, + toward the finish (nought on a twin pipe, which NDH enforces).
    [StructLayout(LayoutKind.Sequential)]
    private struct AuthoredValve
    {
        public int VertexIndex;
        public int Reserved;
        public double FremStaggerM;
        public double ReturStaggerM;
    }

    //sizeof 1104
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

    //sizeof 72
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PipeIdentity
    {
        public int Dn;
        public int Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string System;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string Type;
    }

    //sizeof 1056
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct VertexStraightResult
    {
        public int Status;
        public int Reserved;
        public double BackM;
        public double ForwardM;
        public double MinimumPipeM;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Detail;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int VertexStraightFn(
        in PipeIdentity before, in PipeIdentity after, double turnDegrees, int turnedByAPart,
        out VertexStraightResult result);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JunctionStraightResult
    {
        public int Status;
        public int Reserved;
        public double BackM;
        public double ForwardM;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Detail;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int JunctionStraightFn(
        in PipeIdentity main, in PipeIdentity branch, int branchAtStart, int outlet,
        [MarshalAs(UnmanagedType.LPWStr)] string produkt,
        out JunctionStraightResult result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int BuildPipelineFn(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        [In] RouteVertex[] vertices,
        int vertexCount,
        [In] IdentityBoundary[] boundaries,
        int boundaryCount,
        [In] AuthoredValve[] valves,
        int valveCount,
        out BuildResult result,
        [In, Out] ulong[] vertexCauses);

    static NsDhPipelineBridge()
    {
        NsDhModule.RequireLayout<RouteVertex>(24);
        NsDhModule.RequireLayout<IdentityBoundary>(72);
        NsDhModule.RequireLayout<AuthoredValve>(24);
        NsDhModule.RequireLayout<BuildResult>(1104);
        NsDhModule.RequireLayout<PipeIdentity>(72);
        NsDhModule.RequireLayout<VertexStraightResult>(1056);
    }

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

        //Named by the route's own vertex numbering, exactly as a boundary is.
        AuthoredValve[] valves = new AuthoredValve[route.Valves.Count];
        for (int i = 0; i < valves.Length; i++)
        {
            NdhRouteValve v = route.Valves[i];
            valves[i] = new AuthoredValve
            {
                VertexIndex = v.VertexIndex,
                FremStaggerM = v.FremStaggerM,
                ReturStaggerM = v.ReturStaggerM,
            };
        }

        //ONE SLOT PER VERTEX THE CALLER ASKED FOR. The dbx zeroes them before
        //it builds anything, so a refusal hands back zeroes rather than
        //whatever this array happened to hold.
        ulong[] causes = new ulong[vertices.Length];

        int code = build(name, vertices, vertices.Length, boundaries, boundaries.Length,
                         valves, valves.Length, out BuildResult r, causes);
        (NdhBuildStatus status, string detail) = NsDhModule.Named(code, r.Detail, NdhBuildStatus.BuildFailed);

        return new NdhBuildOutcome(
            status,
            r.PipelineHandle ?? "",
            r.VertexIndex,
            r.SegmentIndex,
            detail,
            causes);
    }

    /// <summary>
    /// The straight everything standing at a vertex takes, under the working
    /// drawing's own catalogue and settings. A refusal is the module's
    /// sentence: the importer cannot place a vertex without it.
    /// </summary>
    public PartStraight At(
        LegacyIdentitySpan before, LegacyIdentitySpan after, double turnDegrees,
        bool turnedByAPart)
    {
        VertexStraightFn ask = NsDhModule.Resolve<VertexStraightFn>(
            NsDhSurface.VertexStraight, "NsDh_VertexStraight");

        return Answered(
            ask(IdentityOf(before), IdentityOf(after), turnDegrees, turnedByAPart ? 1 : 0,
                out VertexStraightResult r),
            r, Describe(before, after, turnDegrees, turnedByAPart));
    }

    /// <summary>
    /// The straight the connection takes out of its main, either side of the
    /// branch point, under the working drawing's own catalogue and settings. A
    /// refusal is the module's sentence: the importer cannot seat a junction
    /// without it.
    /// </summary>
    public JunctionStraight At(
        LegacyIdentitySpan main, LegacyIdentitySpan branch, bool branchAtStart,
        NdhBranchOutlet outlet, string produkt)
    {
        JunctionStraightFn ask = NsDhModule.Resolve<JunctionStraightFn>(
            NsDhSurface.JunctionStraight, "NsDh_JunctionStraight");

        int code = ask(IdentityOf(main), IdentityOf(branch), branchAtStart ? 1 : 0,
                       (int)outlet, produkt, out JunctionStraightResult r);
        (NdhBuildStatus status, string detail) =
            NsDhModule.Named(code, r.Detail, NdhBuildStatus.BuildFailed);
        return status == NdhBuildStatus.Ok
            ? new JunctionStraight(r.BackM, r.ForwardM)
            : throw new InvalidOperationException(
                $"The straight of the '{produkt}' joining " +
                $"{branch.System} {branch.Type} {branch.Dn} to " +
                $"{main.System} {main.Type} {main.Dn} could not be asked ({status}): {detail}");
    }

    private static string Describe(
        LegacyIdentitySpan before, LegacyIdentitySpan after, double turnDegrees,
        bool turnedByAPart)
    {
        string pipe = $"{before.System} {before.Type} {before.Dn}";
        string change = before.System == after.System && before.Type == after.Type && before.Dn == after.Dn
            ? ""
            : $"the change from {pipe} to {after.System} {after.Type} {after.Dn}";
        string turn = turnDegrees <= 0.0
            ? ""
            : turnedByAPart
                ? $"the {turnDegrees:F1} degree elbow of {pipe}"
                : $"the {turnDegrees:F1} degree bend of {pipe}";
        string[] parts = new[] { change, turn }.Where(p => p.Length != 0).ToArray();
        return parts.Length == 0 ? $"the vertex on {pipe}" : string.Join(" and ", parts);
    }

    private static PartStraight Answered(int code, VertexStraightResult r, string what)
    {
        (NdhBuildStatus status, string detail) = NsDhModule.Named(code, r.Detail, NdhBuildStatus.BuildFailed);
        return status == NdhBuildStatus.Ok
            ? new PartStraight(r.BackM, r.ForwardM, r.MinimumPipeM)
            : throw new InvalidOperationException(
                $"The straight of {what} could not be asked ({status}): {detail}");
    }

    private static PipeIdentity IdentityOf(LegacyIdentitySpan s) => new PipeIdentity
    {
        Dn = s.Dn,
        System = NsDhModule.SystemToken(s.System),
        Type = NsDhModule.TypeToken(s.Type),
    };
}
