using System.IO;
using System.Text.Json;

namespace NorsynDistrictZones;

/// <summary>
/// Per-USER NDZ settings — global across every drawing (user's explicit choice), persisted
/// as JSON under <c>%APPDATA%\Norsyn\NorsynDistrictZones\settings.json</c>. Cached in memory
/// and written through on change. Currently just the zone-label text height: set it once and
/// every drawing renders/exports labels at that height.
/// </summary>
internal static class GlobalSettings
{
    private sealed class Data { public double? LabelHeight { get; set; } }

    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Norsyn", "NorsynDistrictZones");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static Data? _cache;
    private static Data Current => _cache ??= Load();

    /// <summary>
    /// Absolute MText height for zone labels, in drawing units. <c>null</c> ⇒ auto
    /// (per-zone: a fraction of the zone's smaller extent). Setting ≤ 0 clears to auto.
    /// </summary>
    public static double? LabelHeight
    {
        get => Current.LabelHeight;
        set { Current.LabelHeight = value is > 0 ? value : null; Save(); }
    }

    private static Data Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) ?? new Data() : new Data(); }
        catch { return new Data(); }
    }

    private static void Save()
    {
        try { Directory.CreateDirectory(Dir); File.WriteAllText(FilePath, JsonSerializer.Serialize(Current)); }
        catch { /* settings are best-effort; never block a command on disk */ }
    }
}
