using System.Text.RegularExpressions;

using IntersectUtilities.UtilsCommon.Enums;   // PSv2: PipeSystemEnum, PipeTypeEnum

using NorsynHydraulicCalc;                     // NHS: SegmentType
using NhsPipeType = NorsynHydraulicCalc.PipeType;

namespace NorsynDistrictZones.Pricing;

/// <summary>
/// THE single owner of the PipeScheduleV2 (PSv2) ↔ NorsynHydraulicShared (NHS)
/// pipe-type mapping, and of the <c>FJV-&lt;Type&gt;-&lt;System&gt;&lt;DN&gt;</c>
/// drawing-layer parser. No other code in this plugin re-implements either —
/// "logic must be singular". (When the cross-repo shared-source extraction in
/// the plan's P12 lands, this file is the lift-and-shift target.)
///
/// Known information-loss point: NHS encodes FL/SL in the type name
/// (e.g. AluPEXFL vs AluPEXSL) but PSv2 <see cref="PipeSystemEnum"/> has no FL/SL
/// axis, so <see cref="ToPsv2System"/> collapses them. Recovering FL/SL from a
/// layer name alone is impossible — callers must supply the segment type
/// (carried as XData on the pipe from the DimV2 export; see the FL/SL blocker).
/// </summary>
public static class PipeTypeTranslator
{
    /// <summary>NHS pipe type → PSv2 system (material level; FL/SL collapse — lossy by design).</summary>
    public static PipeSystemEnum ToPsv2System(NhsPipeType t) => t switch
    {
        NhsPipeType.Stål => PipeSystemEnum.Stål,
        NhsPipeType.PertFlextraFL or NhsPipeType.PertFlextraSL => PipeSystemEnum.PertFlextra,
        NhsPipeType.AluPEXFL or NhsPipeType.AluPEXSL => PipeSystemEnum.AluPex,
        NhsPipeType.Kobber => PipeSystemEnum.Kobberflex,
        NhsPipeType.AquaTherm11 => PipeSystemEnum.AquaTherm11,
        NhsPipeType.Pe => PipeSystemEnum.PE,
        NhsPipeType.FibreFlexFL or NhsPipeType.FibreFlexSL => PipeSystemEnum.FibreFlex,
        _ => PipeSystemEnum.Ukendt,
    };

    /// <summary>
    /// PSv2 system + the FL/SL segment → NHS pipe type. The segment MUST be supplied
    /// for the FL/SL-bearing families (AluPex, PertFlextra, FibreFlex); it cannot be
    /// derived from PSv2 alone. Returns null for PertPIPE (no NHS counterpart).
    /// </summary>
    public static NhsPipeType? ToNhs(PipeSystemEnum sys, SegmentType seg) => sys switch
    {
        PipeSystemEnum.Stål => NhsPipeType.Stål,
        PipeSystemEnum.Kobberflex => NhsPipeType.Kobber,
        PipeSystemEnum.AquaTherm11 => NhsPipeType.AquaTherm11,
        PipeSystemEnum.PE => NhsPipeType.Pe,
        PipeSystemEnum.AluPex => seg == SegmentType.Stikledning ? NhsPipeType.AluPEXSL : NhsPipeType.AluPEXFL,
        PipeSystemEnum.PertFlextra => seg == SegmentType.Stikledning ? NhsPipeType.PertFlextraSL : NhsPipeType.PertFlextraFL,
        PipeSystemEnum.FibreFlex => seg == SegmentType.Stikledning ? NhsPipeType.FibreFlexSL : NhsPipeType.FibreFlexFL,
        _ => (NhsPipeType?)null,
    };

    /// <summary>PSv2 layer system-string token → <see cref="PipeSystemEnum"/> (mirrors PSv2 systemDict).</summary>
    private static readonly (string Token, PipeSystemEnum System)[] SystemTokens =
    {
        // Order longest-first so e.g. "PRTPIPE" wins over a hypothetical "PRT" prefix
        // and "AQTHRM11" is matched whole (its trailing "11" is not the DN).
        ("PRTFLEXL",  PipeSystemEnum.PertFlextra),
        ("FIBREFLEX", PipeSystemEnum.FibreFlex),
        ("AQTHRM11",  PipeSystemEnum.AquaTherm11),
        ("PRTPIPE",   PipeSystemEnum.PertPIPE),
        ("ALUPEX",    PipeSystemEnum.AluPex),
        ("DN",        PipeSystemEnum.Stål),
        ("CU",        PipeSystemEnum.Kobberflex),
        ("PE",        PipeSystemEnum.PE),
    };

    // Strips an optional xref prefix ("<name>|") before the FJV- token.
    private static readonly Regex FjvLayerRx =
        new(@"^(?:.*\|)?FJV-(?<TYPE>[A-Za-zÆØÅæøå]+)-(?<REST>[A-Za-z0-9]+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parse a (possibly xref-prefixed) <c>FJV-&lt;Type&gt;-&lt;System&gt;&lt;DN&gt;</c> layer name.
    /// Returns false for any layer that is not an FJV pipe layer.
    /// </summary>
    public static bool TryParseLayer(string? layer, out PipeSystemEnum system, out PipeTypeEnum type, out int dn)
    {
        system = PipeSystemEnum.Ukendt;
        type = PipeTypeEnum.Ukendt;
        dn = 0;
        if (string.IsNullOrWhiteSpace(layer)) return false;

        var m = FjvLayerRx.Match(layer);
        if (!m.Success) return false;

        if (!Enum.TryParse(m.Groups["TYPE"].Value, ignoreCase: true, out PipeTypeEnum pt))
            return false;
        type = pt;

        string rest = m.Groups["REST"].Value.ToUpperInvariant();
        foreach (var (token, sys) in SystemTokens)
        {
            if (rest.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            {
                string dnPart = rest.Substring(token.Length);
                if (int.TryParse(dnPart, out int parsedDn) && parsedDn > 0)
                {
                    system = sys;
                    dn = parsedDn;
                    return true;
                }
            }
        }
        return false;
    }
}
