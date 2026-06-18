namespace GraphViewV3.Core;

/// <summary>Parses FJV pipe layer names like FJV-TWIN-DN125, FJV-FREM-DN125,
/// FJV-RETUR-DN125, FJV-TWIN-PRTFLEXL50 into (system, size). Pure string logic so it
/// is unit-testable without AutoCAD.</summary>
public static class FjvLayer
{
    public static bool IsFjv(string layer) =>
        layer.StartsWith("FJV", StringComparison.OrdinalIgnoreCase);

    /// <summary>System: TWIN | FREM | RETUR | "" (unknown). Size: e.g. DN125, PRTFLEXL50.</summary>
    public static (string System, string Size) Parse(string layer)
    {
        if (string.IsNullOrWhiteSpace(layer)) return ("", "");
        var parts = layer.Split('-', StringSplitOptions.RemoveEmptyEntries);
        // FJV - <SYSTEM> - <SIZE...> ; size may itself contain '-' historically -> rejoin.
        if (parts.Length < 3) return (parts.Length > 1 ? parts[1] : "", "");
        string system = parts[1].ToUpperInvariant();
        string size = string.Join("-", parts.Skip(2));
        return (system, size);
    }

    /// <summary>Twin pipes vs bonded (separate FREM/RETUR steel pipes).</summary>
    public static string SystemClass(string system) => system switch
    {
        "TWIN" => "Twin",
        "FREM" or "RETUR" => "Bonded",
        _ => "Other",
    };
}
