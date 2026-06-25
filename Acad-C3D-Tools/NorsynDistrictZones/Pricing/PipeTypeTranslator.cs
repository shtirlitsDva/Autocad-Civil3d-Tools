using System.Text.RegularExpressions;

using IntersectUtilities.UtilsCommon.Enums;   // PSv2: PipeSystemEnum, PipeTypeEnum

namespace NorsynDistrictZones.Pricing;

/// <summary>
/// Owner of the <c>FJV-&lt;Type&gt;-&lt;System&gt;&lt;DN&gt;</c> drawing-layer parser, used
/// only to recognise which model-space entities are FJV pipes and read their DN. The
/// authoritative NHS pipe type and FL/SL role are read straight off the pipe's export XData
/// (see <see cref="Acad.PipeReader"/>) — they are NEVER reconstructed from the layer, because
/// pipe type and FL/SL role are independent axes (a Fællesstikledning is an SL-typed pipe in
/// the Fordelingsledning role). Any such reconstruction would silently mis-price it.
/// </summary>
public static class PipeTypeTranslator
{

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
