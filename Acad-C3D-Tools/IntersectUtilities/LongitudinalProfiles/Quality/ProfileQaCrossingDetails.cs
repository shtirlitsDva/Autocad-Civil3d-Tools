using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.LongitudinalProfiles;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IntersectUtilities.LongitudinalProfiles.Quality;

internal readonly record struct QaCrossingDetails(string Diameter, string Material);

// Read source attributes only. Collision geometry continues to come entirely from the drawn block.
internal sealed class QaCrossingDetailsReader : IDisposable
{
    private readonly string[] paths;
    private readonly Dictionary<string, Database> opened = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Result<QaCrossingDetails>> cache = new(StringComparer.OrdinalIgnoreCase);

    private QaCrossingDetailsReader(string[] paths) => this.paths = paths;

    internal static Result<QaCrossingDetailsReader> Create()
    {
        if (DataReferencesOptions.Create() is not DataReferencesOptions references)
            return Result<QaCrossingDetailsReader>.Failure("Project selection cancelled.");
        try
        {
            string[] paths = new DataManager(references).PathToLer().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return paths.Length == 0 ? Result<QaCrossingDetailsReader>.Failure("No LER source files configured.")
                : Result<QaCrossingDetailsReader>.Success(new(paths));
        }
        catch (System.Exception error) { return Result<QaCrossingDetailsReader>.Failure(error.Message); }
    }

    internal Result<QaCrossingDetails> Read(string reference)
    {
        if (cache.TryGetValue(reference, out var prior)) return prior;
        string[] parts = reference.Split(':');
        string handleText = parts[^1].Split('_')[0];
        if (!long.TryParse(handleText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long handle))
            return Remember(reference, Result<QaCrossingDetails>.Failure("Invalid LER source handle."));
        string fileName = parts.Length >= 2 && parts[^2].EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) ? parts[^2] : "";
        string[] matches = fileName.Length == 0 ? paths : paths.Where(p => string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) return Remember(reference, Result<QaCrossingDetails>.Failure("LER source file is missing or ambiguous."));
        return Remember(reference, Open(matches[0]).Bind(database =>
        {
            try
            {
                using var transaction = database.TransactionManager.StartTransaction();
                if (!database.TryGetObjectId(new Handle(handle), out ObjectId id) || id.IsNull || id.IsErased)
                    return Result<QaCrossingDetails>.Failure("LER source entity is missing.");
                if (transaction.GetObject(id, OpenMode.ForRead) is not Entity entity)
                    return Result<QaCrossingDetails>.Failure("LER reference does not identify an entity.");
                return Result<QaCrossingDetails>.Success(ReadEntity(entity));
            }
            catch (System.Exception error) { return Result<QaCrossingDetails>.Failure(error.Message); }
        }));
    }

    internal static QaCrossingDetails ReadEntity(Entity entity)
    {
        string diameterRecipe = Csv.Krydsninger.Diameter(entity.Layer) ?? "";
        string materialRecipe = Csv.Krydsninger.Material(entity.Layer) ?? "";
        string diameter = diameterRecipe.Length == 0 ? "" : Utils.ConstructStringFromPSByRecipe(entity, diameterRecipe);
        string material = materialRecipe.Length == 0 ? "" : Utils.ConstructStringFromPSByRecipe(entity, materialRecipe);
        if (string.IsNullOrWhiteSpace(diameter) || diameter == "0") diameter = Property(entity, "UdvendigDiameter");
        if (string.IsNullOrWhiteSpace(material)) material = Property(entity, "UdvendigMateriale");
        string units = Property(entity, "UdvendigDiameterUnits");
        return new(DiameterText(diameter, units.Length == 0 ? "mm" : units), Clean(material));
    }

    private static string Property(Entity entity, string name) => PropertySetManager.TryReadProperty(entity, name, out object value)
        ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";

    private static string Clean(string value)
    {
        string text = value.Trim();
        return text.Length == 0 || text == "<null>" || text == "<empty string>" || text.Contains('{') ? "?" : text;
    }

    internal static string DiameterText(string value, string units)
    {
        string text = value.Trim().TrimStart('ø', 'Ø');
        if (!double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return Clean(text);
        if (!double.IsFinite(number) || number <= 0 || number == 999) return "?";
        string normalized = units.Trim().ToLowerInvariant();
        if (normalized == "m") number *= 1000;
        else if (normalized == "cm") number *= 10;
        else if (normalized.Length > 0 && normalized != "mm") return $"{number:0.#} {units}";
        return $"{number:0.#} mm";
    }

    private Result<Database> Open(string path)
    {
        if (opened.TryGetValue(path, out var database)) return Result<Database>.Success(database);
        var source = new Database(false, true);
        try
        {
            source.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, "");
            opened.Add(path, source);
            return Result<Database>.Success(source);
        }
        catch (System.Exception error) { source.Dispose(); return Result<Database>.Failure(error.Message); }
    }

    private Result<QaCrossingDetails> Remember(string reference, Result<QaCrossingDetails> value)
    {
        cache[reference] = value;
        return value;
    }

    public void Dispose()
    {
        foreach (var database in opened.Values) database.Dispose();
        opened.Clear();
    }
}
