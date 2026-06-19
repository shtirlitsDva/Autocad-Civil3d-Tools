using System;
using System.Collections.Generic;

namespace IntersectUtilities.GraphWriteV2.Theming;

public enum PalettePreset { Cyan, Amber, Emerald, Violet }

/// <summary>
/// The four palette presets and the factory default theme, mirroring the design package's PRESETS
/// table. Selecting a preset replaces the whole <see cref="ThemeColors"/> block; the editable
/// colors then diverge into a "Custom" palette while the rest keep the preset's values.
/// </summary>
public static class LabelThemePresets
{
    public static readonly IReadOnlyList<PalettePreset> All =
        new[] { PalettePreset.Cyan, PalettePreset.Amber, PalettePreset.Emerald, PalettePreset.Violet };

    public static string DisplayName(PalettePreset p) => p switch
    {
        PalettePreset.Cyan => "Cyan Ice",
        PalettePreset.Amber => "Amber Industrial",
        PalettePreset.Emerald => "Emerald Signal",
        PalettePreset.Violet => "Violet Neon",
        _ => p.ToString(),
    };

    /// <summary>The swatch shown next to the preset in the picker (its frame/border accent).</summary>
    public static string SwatchHex(PalettePreset p) => Colors(p).Frame;

    public static ThemeColors Colors(PalettePreset p) => p switch
    {
        PalettePreset.Cyan => new ThemeColors
        {
            Background = "#0a131c", Fill1 = "#15273a", Fill2 = "#1d3650",
            FillLite1 = "#243a52", FillLite2 = "#2e4865", Frame = "#48d6e8", Divider = "#2f5f74",
            Id = "#7df0ff", Type = "#9fc3d6", Body = "#e3f1f8",
            Serie1 = "#38bdf8", Serie2 = "#fbbf24", Serie3 = "#f472b6", SerieText = "#04121a",
            TileA1 = "#cfe4f0", TileA2 = "#e8f3fa", Ink = "#0c1a26", IdInk = "#0d5f7e",
        },
        PalettePreset.Amber => new ThemeColors
        {
            Background = "#15110a", Fill1 = "#241c0f", Fill2 = "#322611",
            FillLite1 = "#3a2d16", FillLite2 = "#473720", Frame = "#e0a536", Divider = "#6e5526",
            Id = "#ffd36b", Type = "#d8c39a", Body = "#f1e6cf",
            Serie1 = "#56c2ff", Serie2 = "#a3e635", Serie3 = "#fb7185", SerieText = "#1a1305",
            TileA1 = "#ece0c2", TileA2 = "#f6efda", Ink = "#241a09", IdInk = "#8a5810",
        },
        PalettePreset.Emerald => new ThemeColors
        {
            Background = "#08140e", Fill1 = "#102619", Fill2 = "#163524",
            FillLite1 = "#1c3c2a", FillLite2 = "#244c35", Frame = "#34c98a", Divider = "#27684a",
            Id = "#73f0ad", Type = "#a6cfb8", Body = "#dff2e7",
            Serie1 = "#2dd4bf", Serie2 = "#facc15", Serie3 = "#fb7185", SerieText = "#04130b",
            TileA1 = "#d2ecdd", TileA2 = "#e8f5ee", Ink = "#0a1f14", IdInk = "#136a44",
        },
        PalettePreset.Violet => new ThemeColors
        {
            Background = "#120c1c", Fill1 = "#1f1430", Fill2 = "#2b1c43",
            FillLite1 = "#33224f", FillLite2 = "#3f2b60", Frame = "#a855f7", Divider = "#5a3f80",
            Id = "#d9b8ff", Type = "#bda9d6", Body = "#ece1f7",
            Serie1 = "#c084fc", Serie2 = "#f0abfc", Serie3 = "#67e8f9", SerieText = "#160a22",
            TileA1 = "#e2d3f2", TileA2 = "#f0e7fb", Ink = "#190d29", IdInk = "#6a3da0",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(p)),
    };

    /// <summary>The factory default: Glass HUD on the Cyan Ice palette.</summary>
    public static LabelTheme Default() => new()
    {
        Style = LabelStyle.Glass,
        TileMode = SegmentMode.Pop,
        Colors = Colors(PalettePreset.Cyan),
        Series = new SeriesStyle { Show = true, Fill = SerieFill.Filled, Pos = SeriePos.After },
        Fonts = new FontSet { Id = "Courier New" },
        Padding = 9,
    };
}
