using System.Text;
using System.Text.Json;

using Autodesk.AutoCAD.DatabaseServices;

namespace NorsynDistrictZones.Pricing;

/// <summary>
/// Per-drawing persistence for named price catalogs + the active selection, stored as
/// JSON in a chunked Xrecord under the drawing's Named Object Dictionary. Edits are
/// local to the drawing and never touch the NorsynHydraulicShared defaults. All pricing
/// reads the ACTIVE catalog through <see cref="GetActive"/> (seeding defaults on first use).
/// </summary>
internal static class CatalogStore
{
    private const string DictKey = "NDZ_PRICE_CATALOGS";
    private const int ChunkSize = 250;

    private sealed class Persisted
    {
        public string Active { get; set; } = "Default";
        public List<PipePriceCatalog> Catalogs { get; set; } = new();
    }

    public static PipePriceCatalog GetActive(Database db)
    {
        Persisted p = Load(db);
        PipePriceCatalog? cat = p.Catalogs.FirstOrDefault(c =>
            string.Equals(c.Name, p.Active, StringComparison.OrdinalIgnoreCase));
        return cat ?? p.Catalogs.FirstOrDefault() ?? PipePriceCatalog.SeedFromDefaults();
    }

    public static (List<PipePriceCatalog> Catalogs, string Active) LoadAll(Database db)
    {
        Persisted p = Load(db);
        if (p.Catalogs.Count == 0)
        {
            p.Catalogs.Add(PipePriceCatalog.SeedFromDefaults());
            p.Active = p.Catalogs[0].Name;
        }
        return (p.Catalogs, p.Active);
    }

    public static void SaveAll(Database db, IEnumerable<PipePriceCatalog> catalogs, string active)
    {
        var p = new Persisted { Active = active, Catalogs = catalogs.ToList() };
        string json = JsonSerializer.Serialize(p);
        WriteJson(db, json);
    }

    private static Persisted Load(Database db)
    {
        string? json = ReadJson(db);
        if (string.IsNullOrEmpty(json)) return new Persisted();
        try { return JsonSerializer.Deserialize<Persisted>(json) ?? new Persisted(); }
        catch { return new Persisted(); }
    }

    // --- NOD chunked-Xrecord storage ---

    private static void WriteJson(Database db, string json)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        var nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);

        var rb = new ResultBuffer();
        for (int i = 0; i < json.Length; i += ChunkSize)
            rb.Add(new TypedValue((int)DxfCode.Text, json.Substring(i, Math.Min(ChunkSize, json.Length - i))));

        Xrecord xrec;
        if (nod.Contains(DictKey))
        {
            xrec = (Xrecord)tx.GetObject(nod.GetAt(DictKey), OpenMode.ForWrite);
        }
        else
        {
            xrec = new Xrecord();
            nod.SetAt(DictKey, xrec);
            tx.AddNewlyCreatedDBObject(xrec, true);
        }
        xrec.Data = rb;
        tx.Commit();
    }

    private static string? ReadJson(Database db)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        var nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!nod.Contains(DictKey)) { tx.Commit(); return null; }

        var xrec = (Xrecord)tx.GetObject(nod.GetAt(DictKey), OpenMode.ForRead);
        var sb = new StringBuilder();
        if (xrec.Data is not null)
            foreach (TypedValue tv in xrec.Data)
                if (tv.TypeCode == (short)DxfCode.Text) sb.Append(tv.Value as string);
        tx.Commit();
        return sb.ToString();
    }
}
