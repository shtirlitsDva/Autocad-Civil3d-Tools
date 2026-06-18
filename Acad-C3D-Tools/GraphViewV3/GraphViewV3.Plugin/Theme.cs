using System.Windows.Media;

namespace GraphViewV3;

/// <summary>Shared dark palette + brush helpers for the GraphViewV3 visuals.</summary>
internal static class Theme
{
    public static readonly Brush BgDeep = Hex("#1E2124");
    public static readonly Brush BgPanel = Hex("#2A2D31");
    public static readonly Brush BgSurface = Hex("#33373C");
    public static readonly Brush BgElevated = Hex("#3D4248");
    public static readonly Brush BorderSubtle = Hex("#262A2E");
    public static readonly Brush BorderDefault = Hex("#4A5159");
    public static readonly Brush EdgeBrush = Hex("#5C6470");
    public static readonly Brush ErrBrush = Hex("#B94A4A");
    public static readonly Brush TextPrimary = Hex("#E8E8E8");
    public static readonly Brush TextMuted = Hex("#8A9099");
    public static readonly Brush CompStroke = Hex("#9D7CE0");
    public static readonly Brush OkBrush = Hex("#6FBF8B");

    public static SolidColorBrush Hex(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex)!;
        var b = new SolidColorBrush(c); b.Freeze(); return b;
    }

    /// <summary>Stable colour for a pipe size (shared by graph nodes and dashboard bars).</summary>
    public static Brush SizeBrush(string size)
    {
        if (string.IsNullOrEmpty(size)) return OkBrush;
        uint h = 2166136261;
        foreach (char c in size) { h ^= c; h *= 16777619; }
        return new SolidColorBrush(FromHsv(h % 360, 0.55, 0.85));
    }

    public static Color FromHsv(double h, double s, double v)
    {
        int hi = (int)(h / 60) % 6;
        double f = h / 60 - Math.Floor(h / 60);
        double p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        double r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
