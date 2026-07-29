using IntersectUtilities.UtilsCommon.DataManager.CsvData;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// Two-stage block picker over "FJV Dynamiske Komponenter.csv": first the pipe system
/// (<c>SysNavn</c>), then the component names within it. Splitting it keeps each grid scannable —
/// the flat catalogue is around 80 names and <see cref="StringGridForm"/> has no search box.
/// </summary>
internal static class ComponentCataloguePicker
{
    private const string AllSystems = "* Alle *";

    /// <summary>
    /// CSV rows whose SysNavn is one of these are scaffolding, not selectable systems.
    /// </summary>
    private static bool IsSystemSentinel(string sysNavn) =>
        sysNavn.Length == 0 ||
        sysNavn.StartsWith('#') ||
        sysNavn.StartsWith('$') ||
        string.Equals(sysNavn, "Ukendt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifies the catalogue is reachable. The CSV lives on the X: drive, so a disconnected share
    /// surfaces here as a readable message instead of an exception mid-command.
    /// </summary>
    internal static bool TryLoad(out string message)
    {
        try
        {
            if (Csv.FjvDynamicComponents.Rows.Count == 0)
            {
                message = "FJV Dynamiske Komponenter.csv er tom.";
                return false;
            }

            message = string.Empty;
            return true;
        }
        catch (System.Exception exception)
        {
            message = $"Kan ikke læse FJV Dynamiske Komponenter.csv: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Runs both picker stages. Returns false when the user escapes either one.
    /// </summary>
    internal static bool TryPick(out string blockName, out string message)
    {
        blockName = string.Empty;

        List<string> systems = Csv.FjvDynamicComponents.Rows
            .Select(row => CsvDataSource.Col(row, FjvDynamicComponents.Columns.SysNavn))
            .Where(x => !IsSystemSentinel(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        systems.Insert(0, AllSystems);

        string? system = StringGridFormCaller.Call(systems, "VÆLG SYSTEM:");
        if (system is null)
        {
            message = "PLACELONGITUDINALBLOCK annulleret.";
            return false;
        }

        List<string> blockNames = BlockNamesFor(system);
        if (blockNames.Count == 0)
        {
            // Guarded because StringGridFormCaller.Call throws on an empty list.
            message = $"Ingen komponenter fundet for system '{system}'.";
            return false;
        }

        string? selected = StringGridFormCaller.Call(blockNames, "VÆLG KOMPONENT:");
        if (selected is null)
        {
            message = "PLACELONGITUDINALBLOCK annulleret.";
            return false;
        }

        blockName = selected;
        message = string.Empty;
        return true;
    }

    private static List<string> BlockNamesFor(string system)
    {
        if (string.Equals(system, AllSystems, StringComparison.Ordinal))
        {
            return Csv.FjvDynamicComponents.AllNavne()
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return Csv.FjvDynamicComponents.Rows
            .Where(row => string.Equals(
                CsvDataSource.Col(row, FjvDynamicComponents.Columns.SysNavn),
                system,
                StringComparison.OrdinalIgnoreCase))
            .Select(row => CsvDataSource.Col(row, FjvDynamicComponents.Columns.Navn))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
