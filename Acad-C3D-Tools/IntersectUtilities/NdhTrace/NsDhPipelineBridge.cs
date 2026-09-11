using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Runtime.InteropServices;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Builds pipelines through the NDH district-heating module's flat C export
/// NsDh_BuildPipeline (contract: NorsynDrawingTools
/// src/NorsynDistrictHeatingObjects/Api/NsDhPipelineBridge.h).
///
/// The module is never loaded or [DllImport]ed here: that would pin the dbx and
/// break its hot reload. The exports are looked up in the module AutoCAD already
/// has mapped, on every call, so no pointer outlives a reload.
/// </summary>
internal sealed class NsDhPipelineBridge : INdhPipelineBuilder
{
    private const string DbxModule = "NSNorsynDistrictHeating.dbx";

    //Must match kNsDhPipelineBuildVersion in NsDhPipelineBridge.h.
    private const int ExpectedBuildVersion = 1;

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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int BuildVersionFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int BuildPipelineFn(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        [In] RouteVertex[] vertices,
        int vertexCount,
        [In] IdentityBoundary[] boundaries,
        int boundaryCount,
        out BuildResult result);

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string moduleName);

    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);

    public NdhBuildOutcome Build(string name, NdhRoute route)
    {
        BuildPipelineFn build = Resolve<BuildPipelineFn>("NsDh_BuildPipeline");

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
                System = SystemToken(b.System),
                Type = TypeToken(b.Type),
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
    /// The export, from the module as it is mapped right now, after checking
    /// the module speaks the layout this file mirrors.
    /// </summary>
    private static TDelegate Resolve<TDelegate>(string export) where TDelegate : Delegate
    {
        IntPtr module = GetModuleHandleW(DbxModule);
        if (module == IntPtr.Zero)
            throw new InvalidOperationException(
                $"{DbxModule} is not loaded. Load the district-heating module and try again.");

        IntPtr versionProc = GetProcAddress(module, "NsDh_PipelineBuildVersion");
        if (versionProc == IntPtr.Zero)
            throw new InvalidOperationException(
                $"{DbxModule} has no pipeline build export. Update the district-heating module.");

        int actual = Marshal.GetDelegateForFunctionPointer<BuildVersionFn>(versionProc)();
        if (actual != ExpectedBuildVersion)
            throw new InvalidOperationException(
                $"{DbxModule} exposes pipeline build version {actual}, but this build " +
                $"expects {ExpectedBuildVersion}. Rebuild so the module and IntersectUtilities match.");

        IntPtr proc = GetProcAddress(module, export);
        if (proc == IntPtr.Zero)
            throw new InvalidOperationException($"{DbxModule} does not export {export}.");
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(proc);
    }

    /// <summary>The catalogue's edit token for a system: the enumerator's name, ASCII-spelled.</summary>
    private static string SystemToken(PipeSystemEnum system) => system switch
    {
        PipeSystemEnum.Stål => "Staal",
        PipeSystemEnum.Kobberflex => "Kobberflex",
        PipeSystemEnum.AluPex => "AluPex",
        PipeSystemEnum.PertFlextra => "PertFlextra",
        PipeSystemEnum.PertPIPE => "PertPIPE",
        PipeSystemEnum.AquaTherm11 => "AquaTherm11",
        PipeSystemEnum.PE => "PE",
        PipeSystemEnum.FibreFlex => "FibreFlex",
        _ => throw new ArgumentOutOfRangeException(nameof(system), system, "No pipe system."),
    };

    /// <summary>
    /// Twin is one run; everything else is the bonded pair, which the new
    /// pipeline models as ONE Enkelt run (both carriers).
    /// </summary>
    private static string TypeToken(PipeTypeEnum type) => type switch
    {
        PipeTypeEnum.Twin => "Twin",
        PipeTypeEnum.Frem or PipeTypeEnum.Retur or PipeTypeEnum.Enkelt => "Enkelt",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No pipe type."),
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
