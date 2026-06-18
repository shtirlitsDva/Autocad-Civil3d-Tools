using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IntersectUtilities.GraphWriteV2.Theming;

/// <summary>
/// Pre-compensates label colors for the GRAPHWRITEV2 HTML viewer, which renders the whole SVG under
/// <c>filter: invert(1) hue-rotate(180deg)</c> to fake dark mode. That filter chain
/// <c>T = hue-rotate(180°) ∘ invert(1)</c> is its own inverse (T∘T = identity), so to make the viewer
/// display the color the user picked in the Theme Designer, the DOT must carry <c>T(picked)</c> —
/// the viewer then maps it back to the picked color. This applies the same transform to every
/// <c>#rrggbb</c> in a label's markup. The Theme Designer preview is NOT transformed: it shows the
/// picked colors directly, which is exactly the final on-screen appearance.
/// </summary>
internal static class ViewerColorInvert
{
    // SVG hue-rotate(180deg) matrix (cos=-1, sin=0). Rows preserve white: each sums to 1.
    private static readonly double[,] H =
    {
        { -0.574, 1.430,  0.144 },
        {  0.426, 0.430,  0.144 },
        {  0.426, 1.430, -0.856 },
    };

    private static readonly Regex HexColor = new("#[0-9a-fA-F]{6}", RegexOptions.Compiled);

    /// <summary>Transform every #rrggbb color in the markup to its viewer pre-compensated value.</summary>
    public static string InvertMarkupColors(string markup) =>
        HexColor.Replace(markup, m => Invert(m.Value));

    /// <summary>T(hex): invert each channel, then apply the hue-rotate(180°) matrix; clamp to [0,255].</summary>
    public static string Invert(string hex)
    {
        if (!TryParse(hex, out double r, out double g, out double b)) return hex;

        // invert(1)
        r = 1 - r; g = 1 - g; b = 1 - b;

        // hue-rotate(180deg)
        double or = H[0, 0] * r + H[0, 1] * g + H[0, 2] * b;
        double og = H[1, 0] * r + H[1, 1] * g + H[1, 2] * b;
        double ob = H[2, 0] * r + H[2, 1] * g + H[2, 2] * b;

        return $"#{Channel(or)}{Channel(og)}{Channel(ob)}";
    }

    private static bool TryParse(string hex, out double r, out double g, out double b)
    {
        r = g = b = 0;
        if (string.IsNullOrEmpty(hex) || hex.Length != 7 || hex[0] != '#') return false;
        if (!int.TryParse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int ri)) return false;
        if (!int.TryParse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int gi)) return false;
        if (!int.TryParse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int bi)) return false;
        r = ri / 255.0; g = gi / 255.0; b = bi / 255.0;
        return true;
    }

    private static string Channel(double v)
    {
        int i = (int)Math.Round(Math.Clamp(v, 0.0, 1.0) * 255.0);
        return i.ToString("x2", CultureInfo.InvariantCulture);
    }
}
