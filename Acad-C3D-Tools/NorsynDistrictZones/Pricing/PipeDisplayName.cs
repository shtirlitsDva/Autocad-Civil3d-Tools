using NhsPipeType = NorsynHydraulicCalc.PipeType;

namespace NorsynDistrictZones.Pricing;

/// <summary>
/// Human-friendly dimension label for the price breakdown, e.g. "AluPEX 040", "DN 050".
/// Collapses the NHS FL/SL axis to one system name — DISPLAY ONLY; pricing always keys on the
/// authoritative NHS type. NDZ-local because PipeScheduleV2 isn't referenced here and its
/// GetSizePrefix yields "Ø"/"DN", not these brand names. Only "DN" and "AluPEX" are confirmed
/// against the user's reference; the rest are best-guess and may need adjusting.
/// </summary>
public static class PipeDisplayName
{
    /// <summary>System name shown in the breakdown, with FL/SL collapsed.</summary>
    public static string System(NhsPipeType type) => type switch
    {
        NhsPipeType.Stål => "DN",
        NhsPipeType.AluPEXFL or NhsPipeType.AluPEXSL => "AluPEX",
        NhsPipeType.PertFlextraFL or NhsPipeType.PertFlextraSL => "PertFlextra",
        NhsPipeType.Kobber => "Cu",
        NhsPipeType.AquaTherm11 => "AquaTherm",
        NhsPipeType.Pe => "PE",
        NhsPipeType.FibreFlexFL or NhsPipeType.FibreFlexSL => "FibreFlex",
        _ => type.ToString(),
    };

    /// <summary>Row label "{system} {DN:000}" — the single source of the label format.</summary>
    public static string Label(string system, int dn) => $"{system} {dn:000}";

    /// <summary>Row label from an NHS type (collapses FL/SL): "AluPEX 040", "DN 050".</summary>
    public static string Label(NhsPipeType type, int dn) => Label(System(type), dn);
}
