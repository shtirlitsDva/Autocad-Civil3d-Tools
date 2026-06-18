using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.GraphWriteV2.Theming;

/// <summary>
/// Persists the single active <see cref="LabelTheme"/> as JSON next to the other IntersectUtilities
/// user settings (same AppData convention as the BPv2 sequence store). The whole palette is written,
/// so a theme saved under any style reloads losslessly. <see cref="Load"/> never throws — a missing
/// or unreadable file yields the factory default, surfaced on the command line so the fallback is not
/// silent.
/// </summary>
public static class LabelThemeStore
{
    private static readonly string ThemePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Autodesk", "ApplicationPlugins", "IntersectUtilities",
        "graphwritev2_label_theme.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Path => ThemePath;

    public static LabelTheme Load()
    {
        if (!File.Exists(ThemePath))
            return LabelThemePresets.Default();

        try
        {
            var json = File.ReadAllText(ThemePath);
            var theme = JsonSerializer.Deserialize<LabelTheme>(json, JsonOptions);
            if (theme is null)
            {
                prdDbg($"GRAPHWRITEV2: label theme at {ThemePath} was empty; using default.");
                return LabelThemePresets.Default();
            }
            // Defensive: deserialization of a partial/older file can leave nested objects null.
            theme.Colors ??= new ThemeColors();
            theme.Series ??= new SeriesStyle();
            theme.Fonts ??= new FontSet();
            return theme;
        }
        catch (Exception ex)
        {
            prdDbg($"GRAPHWRITEV2: failed to read label theme at {ThemePath}; using default.\n{ex.Message}");
            return LabelThemePresets.Default();
        }
    }

    public static void Save(LabelTheme theme)
    {
        var dir = System.IO.Path.GetDirectoryName(ThemePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(theme, JsonOptions);
        File.WriteAllText(ThemePath, json);
    }

    /// <summary>Serialize a theme to an arbitrary path (the "Export theme…" button).</summary>
    public static void Export(LabelTheme theme, string path)
    {
        var json = JsonSerializer.Serialize(theme, JsonOptions);
        File.WriteAllText(path, json);
    }
}
