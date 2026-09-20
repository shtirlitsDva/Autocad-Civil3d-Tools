using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// The NDH district-heating module as this assembly reaches it: flat C exports
/// looked up by name in the module AutoCAD already has mapped (contract:
/// NorsynDrawingTools src/NorsynDistrictHeatingObjects/Api/NsDhPipelineBridge.h).
///
/// The module is never loaded or [DllImport]ed here: that would pin the dbx and
/// break its hot reload. Every export is resolved on every call, so no pointer
/// outlives a reload, and every surface's version is checked before its export
/// is handed out.
/// </summary>
internal static class NsDhModule
{
    public const string DbxModule = "NSNorsynDistrictHeating.dbx";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VersionFn();

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string moduleName);

    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);

    /// <summary>
    /// The export, from the module as it is mapped right now, after checking the
    /// module speaks the layout this assembly mirrors for that surface. Throws
    /// <see cref="InvalidOperationException"/> with a sentence for the drafter
    /// when the module is not loaded or speaks another version.
    /// </summary>
    public static TDelegate Resolve<TDelegate>(NsDhSurface surface, string export) where TDelegate : Delegate
    {
        IntPtr module = VerifiedModule(surface);
        IntPtr proc = GetProcAddress(module, export);
        if (proc == IntPtr.Zero)
            throw new InvalidOperationException($"{DbxModule} does not export {export}.");
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(proc);
    }

    /// <summary>Throws exactly as <see cref="Resolve{TDelegate}"/> would for this surface.</summary>
    public static void Verify(NsDhSurface surface) => VerifiedModule(surface);

    private static IntPtr VerifiedModule(NsDhSurface surface)
    {
        IntPtr module = GetModuleHandleW(DbxModule);
        if (module == IntPtr.Zero)
            throw new InvalidOperationException(
                $"{DbxModule} is not loaded. Load the district-heating module and try again.");

        IntPtr versionProc = GetProcAddress(module, surface.VersionExport);
        if (versionProc == IntPtr.Zero)
            throw new InvalidOperationException(
                $"{DbxModule} has no {surface.Name} export. Update the district-heating module.");

        int actual = Marshal.GetDelegateForFunctionPointer<VersionFn>(versionProc)();
        if (actual != surface.ExpectedVersion)
            throw new InvalidOperationException(
                $"{DbxModule} exposes {surface.Name} version {actual}, but this build " +
                $"expects {surface.ExpectedVersion}. Rebuild so the module and IntersectUtilities match.");
        return module;
    }

    /// <summary>
    /// Throws when the mirror <typeparamref name="T"/> does not marshal to the
    /// size the header static_asserts for its counterpart. Every bridge checks
    /// its mirrors with this in its static constructor: a drift is a build
    /// defect, caught before any call can corrupt memory.
    /// </summary>
    public static void RequireLayout<T>(int headerSize) where T : struct
    {
        int actual = Marshal.SizeOf<T>();
        if (actual != headerSize)
            throw new InvalidOperationException(
                $"{typeof(T).Name} marshals to {actual} bytes, the NDH header says {headerSize}.");
    }

    /// <summary>
    /// A returned status code as the header names it. A code this build does
    /// not know is still a refusal: it reads as <paramref name="unknown"/>, and
    /// its number is kept in the detail so it is not lost.
    /// </summary>
    public static (TStatus Status, string Detail) Named<TStatus>(int code, string? detail, TStatus unknown)
        where TStatus : struct, Enum
    {
        string said = detail ?? "";
        return Enum.IsDefined(typeof(TStatus), code)
            ? ((TStatus)Enum.ToObject(typeof(TStatus), code), said)
            : (unknown, $"(status {code}) {said}");
    }

    /// <summary>The catalogue's edit token for a system: the enumerator's name, ASCII-spelled.</summary>
    public static string SystemToken(PipeSystemEnum system) => system switch
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

    /// <summary>The system an edit token names; false for a token no system has.</summary>
    public static bool TrySystemOfToken(string token, out PipeSystemEnum system)
    {
        foreach (PipeSystemEnum candidate in Enum.GetValues<PipeSystemEnum>())
        {
            if (candidate == PipeSystemEnum.Ukendt) continue;
            if (SystemToken(candidate) != token) continue;
            system = candidate;
            return true;
        }
        system = PipeSystemEnum.Ukendt;
        return false;
    }

    /// <summary>
    /// Twin is one run; everything else is the bonded pair, which the new
    /// pipeline models as ONE Enkelt run (both carriers).
    /// </summary>
    public static string TypeToken(PipeTypeEnum type) => type switch
    {
        PipeTypeEnum.Twin => "Twin",
        PipeTypeEnum.Frem or PipeTypeEnum.Retur or PipeTypeEnum.Enkelt => "Enkelt",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No pipe type."),
    };

    public static string TypeToken(bool twin) => twin ? "Twin" : "Enkelt";

    private static readonly NdhRun[] TwinRuns = { NdhRun.Twin };
    private static readonly NdhRun[] BondedRuns = { NdhRun.Frem, NdhRun.Retur };

    /// <summary>
    /// THE RUNS A CONSTRUCTION HAS. A Twin pipeline has one and it is
    /// <see cref="NdhRun.Twin"/>; a bonded one has two, and one authored cause
    /// yields a SEPARATE, independently overridable component on each - so a
    /// component named on a bonded pipeline is two rows, never one.
    ///
    /// It sits beside <see cref="TypeToken(PipeTypeEnum)"/> because it is the
    /// same kind of statement about the same enum: what this construction IS,
    /// said once, rather than a decision each caller re-takes.
    /// </summary>
    public static IReadOnlyList<NdhRun> RunsOf(PipeTypeEnum type) => type switch
    {
        PipeTypeEnum.Twin => TwinRuns,
        PipeTypeEnum.Frem or PipeTypeEnum.Retur or PipeTypeEnum.Enkelt => BondedRuns,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No pipe type."),
    };
}

/// <summary>
/// One versioned surface of the NDH bridge: the export that reports its version
/// and the version this assembly mirrors. The constants must move together with
/// their counterparts in NsDhPipelineBridge.h.
/// </summary>
internal sealed record NsDhSurface(string Name, string VersionExport, int ExpectedVersion)
{
    //kNsDhPipelineBuildVersion
    //2: the build hands back the durable cause of every vertex the caller
    //authored, which is how a component is named. Until 2 nothing outside the
    //dbx could learn one, so the override lane could not be addressed at all.
    public static readonly NsDhSurface Build = new("pipeline build", "NsDh_PipelineBuildVersion", 2);
    //kNsDhChangeStraightVersion (the elbow straight rides on it)
    public static readonly NsDhSurface ChangeStraight = new("change straight", "NsDh_ChangeStraightVersion", 1);
    //kNsDhBranchConnectVersion
    public static readonly NsDhSurface BranchConnect = new("branch connect", "NsDh_BranchConnectVersion", 1);
    //kNsDhDrawingSettingsVersion
    //2: the surface gained the FITTING RULE SHEET (NsDh_SetFittingRules /
    //NsDh_ReadFittingRules). The producer and the series matrix are unchanged;
    //a caller that uses neither of the new two still meets the bump, because a
    //version is what a whole surface agrees on.
    public static readonly NsDhSurface DrawingSettings = new("drawing settings", "NsDh_DrawingSettingsVersion", 2);
    //kNsDhPipelineReadVersion: 3 added NsDh_ReadPipelineConnections; 4 gave
    //NsDhPlanIssue the complaint's MEASURE as a number, so a caller can act on
    //it instead of reading it back out of the sentence.
    public static readonly NsDhSurface PipelineRead = new("pipeline read", "NsDh_PipelineReadVersion", 4);
    //kNsDhPipelineModifyVersion: the door for changing a pipeline that already
    //stands, which the import's repair pass slides a tee through. 2 added the
    //fitting-selection arm - which part one component uses - with no layout
    //change; the bump is so a newer caller meets an older dbx at this check
    //rather than at an unknown edit kind. 3 gave every override arm the other
    //two thirds of a component's name - its cause kind and its run role -
    //which until then the dbx filled in as Elbow and Twin for all of them.
    public static readonly NsDhSurface PipelineModify = new("pipeline modify", "NsDh_PipelineModifyVersion", 3);

    /// <summary>Every surface NDHFROMFJV uses; probed before the import touches anything.</summary>
    public static readonly NsDhSurface[] UsedByImport =
        [Build, ChangeStraight, BranchConnect, DrawingSettings, PipelineRead, PipelineModify];
}
