using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IntersectUtilities.LongitudinalProfiles.Quality;

internal static class QaCrossingPolicy
{
    private static readonly Regex Voltage = new(@"(?<![\d.,])(?<value>\d+(?:[.,]\d+)?)\s*kV(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CommunicationOrLighting = new(@"(?<![\p{L}\p{N}])(?:KOM(?:MUNIKATION\p{L}*)?|VBL)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EncodedLowVoltage = new(@"(?<![\p{L}\p{N}])(?:EL[_-])?04kV(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex OutOfService = new(@"(?<![\p{L}\p{N}])(?:UAD|ude\s+af\s+drift)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool IncludeCrossing(bool unknownDepth, string layer, string description)
    {
        if (OutOfService.IsMatch(layer) || OutOfService.IsMatch(description)) return false;
        return !unknownDepth || (!IsBlacklisted(layer) && !IsBlacklisted(description));
    }

    internal static bool ReviewUnknownDepth(string layer, string description) => IncludeCrossing(true, layer, description);

    private static bool IsBlacklisted(string text)
    {
        // Match utility identity, not the shared clearance category (EL_04 also covers unspecified cables).
        if (text.Contains("TELE", StringComparison.OrdinalIgnoreCase)
            || text.Contains("vejbelys", StringComparison.OrdinalIgnoreCase)
            || CommunicationOrLighting.IsMatch(text) || EncodedLowVoltage.IsMatch(text)) return true;
        foreach (Match match in Voltage.Matches(text))
            if (double.TryParse(match.Groups["value"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double voltage) && voltage == 0.4) return true;
        return false;
    }
}
