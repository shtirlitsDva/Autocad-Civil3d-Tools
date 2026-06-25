using System.IO;
using System.Text.Json;

namespace NorsynDistrictZones;

/// <summary>
/// Per-USER NDZ settings — global across every drawing (user's explicit choice), persisted
/// as JSON under <c>%APPDATA%\Norsyn\NorsynDistrictZones\settings.json</c>. Cached in memory
/// and written through on change. Holds the zone-label text height and the zone fill
/// transparency: set once and every drawing renders accordingly.
/// </summary>
internal static class GlobalSettings
{
    /// <summary>Zone fill transparency (%) when the user has never set one — ≈ alpha 110, the original look.</summary>
    public const int DefaultTransparencyPercent = 57;

    private sealed class Data
    {
        public double? LabelHeight { get; set; }
        public int? ZoneTransparencyPercent { get; set; }
    }

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

    /// <summary>
    /// Zone fill transparency in percent (0 = opaque, 90 = faintest — AutoCAD's range).
    /// Falls back to <see cref="DefaultTransparencyPercent"/> until the user sets one.
    /// </summary>
    public static int ZoneTransparencyPercent
    {
        get => Current.ZoneTransparencyPercent ?? DefaultTransparencyPercent;
        set { Current.ZoneTransparencyPercent = Math.Clamp(value, 0, 90); Save(); }
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
