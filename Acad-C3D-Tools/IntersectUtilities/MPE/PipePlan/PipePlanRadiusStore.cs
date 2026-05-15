using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal enum PipePlanRadiusSource
{
    Default,
    Override,
    Missing
}

internal sealed record PipePlanRadiusEntry(
    PipeSystemEnum System,
    PipeTypeEnum Type,
    int Dn,
    double Radius,
    PipePlanRadiusSource Source);

internal static class PipePlanRadiusStore
{
    private const string NodDictionaryName = "PIPEPLAN_RADII";

    private static readonly IReadOnlyList<(PipeSystemEnum System, PipeTypeEnum Type)> AcceptedCombos =
    [
        (PipeSystemEnum.Stål, PipeTypeEnum.Twin),
        (PipeSystemEnum.AluPex, PipeTypeEnum.Twin),
        (PipeSystemEnum.AluPex, PipeTypeEnum.Frem),
        (PipeSystemEnum.AluPex, PipeTypeEnum.Retur),
    ];

    private static readonly Dictionary<(PipeSystemEnum, PipeTypeEnum, int), double> Defaults = new()
    {
        { (PipeSystemEnum.Stål, PipeTypeEnum.Twin, 50), 36.0 },
        { (PipeSystemEnum.Stål, PipeTypeEnum.Twin, 100), 68.0 },
        { (PipeSystemEnum.Stål, PipeTypeEnum.Twin, 150), 101.0 },
        { (PipeSystemEnum.Stål, PipeTypeEnum.Twin, 200), 132.0 },
        { (PipeSystemEnum.Stål, PipeTypeEnum.Twin, 250), 164.0 },
    };

    public static IReadOnlyList<(PipeSystemEnum System, PipeTypeEnum Type)> GetAcceptedCombos() => AcceptedCombos;

    public static bool IsAcceptedCombo(PipeSystemEnum system, PipeTypeEnum type)
    {
        foreach (var (s, t) in AcceptedCombos)
        {
            if (s == system && t == type) return true;
        }

        return false;
    }

    public static bool TryGet(Database db, PipeSystemEnum system, PipeTypeEnum type, int dn, out double radius)
    {
        radius = 0.0;
        if (TryReadOverride(db, system, type, dn, out double overrideValue) && overrideValue > 0.0)
        {
            radius = overrideValue;
            return true;
        }

        if (Defaults.TryGetValue((system, type, dn), out double defaultValue) && defaultValue > 0.0)
        {
            radius = defaultValue;
            return true;
        }

        return false;
    }

    public static void Set(Database db, PipeSystemEnum system, PipeTypeEnum type, int dn, double radius)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            DBDictionary radii = GetOrCreateDictionary(nod, tx);
            string key = MakeKey(system, type, dn);
            ResultBuffer payload = new(new TypedValue((int)DxfCode.Real, radius));

            if (radii.Contains(key))
            {
                Xrecord existing = (Xrecord)tx.GetObject(radii.GetAt(key), OpenMode.ForWrite);
                existing.Data = payload;
            }
            else
            {
                Xrecord record = new() { Data = payload };
                radii.SetAt(key, record);
                tx.AddNewlyCreatedDBObject(record, add: true);
            }

            tx.Commit();
        }
        catch
        {
            tx.Abort();
            throw;
        }
    }

    public static void ResetToDefault(Database db, PipeSystemEnum system, PipeTypeEnum type, int dn)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(NodDictionaryName))
            {
                tx.Commit();
                return;
            }

            DBDictionary radii = (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForWrite);
            string key = MakeKey(system, type, dn);
            if (radii.Contains(key))
            {
                DBObject child = tx.GetObject(radii.GetAt(key), OpenMode.ForWrite);
                radii.Remove(key);
                child.Erase();
            }

            tx.Commit();
        }
        catch
        {
            tx.Abort();
            throw;
        }
    }

    public static IReadOnlyList<PipePlanRadiusEntry> EnumerateAll(Database db)
    {
        Dictionary<(PipeSystemEnum, PipeTypeEnum, int), double> overrides = ReadAllOverrides(db);
        List<PipePlanRadiusEntry> entries = new();

        foreach (var (system, type) in AcceptedCombos)
        {
            IEnumerable<int> dns;
            try
            {
                dns = PipeScheduleV2.PipeScheduleV2.ListAllDnsForPipeSystemType(system, type);
            }
            catch
            {
                dns = Enumerable.Empty<int>();
            }

            foreach (int dn in dns.OrderBy(d => d))
            {
                var key = (system, type, dn);
                if (overrides.TryGetValue(key, out double overrideValue) && overrideValue > 0.0)
                {
                    entries.Add(new PipePlanRadiusEntry(system, type, dn, overrideValue, PipePlanRadiusSource.Override));
                    continue;
                }

                if (Defaults.TryGetValue(key, out double defaultValue) && defaultValue > 0.0)
                {
                    entries.Add(new PipePlanRadiusEntry(system, type, dn, defaultValue, PipePlanRadiusSource.Default));
                    continue;
                }

                entries.Add(new PipePlanRadiusEntry(system, type, dn, 0.0, PipePlanRadiusSource.Missing));
            }
        }

        return entries;
    }

    private static bool TryReadOverride(Database db, PipeSystemEnum system, PipeTypeEnum type, int dn, out double radius)
    {
        radius = 0.0;
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(NodDictionaryName))
            {
                tx.Commit();
                return false;
            }

            DBDictionary radii = (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForRead);
            string key = MakeKey(system, type, dn);
            if (!radii.Contains(key))
            {
                tx.Commit();
                return false;
            }

            Xrecord record = (Xrecord)tx.GetObject(radii.GetAt(key), OpenMode.ForRead);
            ResultBuffer? buffer = record.Data;
            tx.Commit();

            if (buffer is null) return false;
            TypedValue[] values = buffer.AsArray();
            if (values.Length == 0 || values[0].Value is not double value) return false;

            radius = value;
            return true;
        }
        catch
        {
            tx.Abort();
            throw;
        }
    }

    private static Dictionary<(PipeSystemEnum, PipeTypeEnum, int), double> ReadAllOverrides(Database db)
    {
        Dictionary<(PipeSystemEnum, PipeTypeEnum, int), double> result = new();
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(NodDictionaryName))
            {
                tx.Commit();
                return result;
            }

            DBDictionary radii = (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in radii)
            {
                if (!TryParseKey(entry.Key, out PipeSystemEnum system, out PipeTypeEnum type, out int dn))
                {
                    continue;
                }

                Xrecord record = (Xrecord)tx.GetObject(entry.Value, OpenMode.ForRead);
                ResultBuffer? buffer = record.Data;
                if (buffer is null) continue;
                TypedValue[] values = buffer.AsArray();
                if (values.Length == 0 || values[0].Value is not double value) continue;

                result[(system, type, dn)] = value;
            }

            tx.Commit();
            return result;
        }
        catch
        {
            tx.Abort();
            throw;
        }
    }

    private static DBDictionary GetOrCreateDictionary(DBDictionary nod, Transaction tx)
    {
        if (nod.Contains(NodDictionaryName))
        {
            return (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForWrite);
        }

        nod.UpgradeOpen();
        DBDictionary radii = new();
        nod.SetAt(NodDictionaryName, radii);
        tx.AddNewlyCreatedDBObject(radii, add: true);
        return radii;
    }

    private static string MakeKey(PipeSystemEnum system, PipeTypeEnum type, int dn)
    {
        return $"{system}.{type}.{dn}";
    }

    private static bool TryParseKey(string key, out PipeSystemEnum system, out PipeTypeEnum type, out int dn)
    {
        system = default;
        type = default;
        dn = 0;
        string[] parts = key.Split('.');
        if (parts.Length != 3) return false;
        if (!Enum.TryParse(parts[0], out system)) return false;
        if (!Enum.TryParse(parts[1], out type)) return false;
        if (!int.TryParse(parts[2], out dn)) return false;
        return true;
    }
}
