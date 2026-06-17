using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.MPE.PipePlanDE;

internal sealed record PipePlanDEParameterEntry(
    int Dn,
    string Label,
    PipePlanDEParameters Parameters,
    bool IsOverride);

/// <summary>
/// Per-drawing overrides of the Regel-Grabenprofil parameters, stored in the
/// Named Objects Dictionary under "PIPEPLANDE_PARAMS" — one Xrecord per DN row
/// carrying the ten Real values in column order. Mirrors the storage pattern of
/// <c>PipePlanRadiusStore</c>. The effective value for a DN is the override when
/// present, otherwise the built-in <see cref="PipePlanDEStandardTable"/> default.
/// </summary>
internal static class PipePlanDEParameterStore
{
    private const string NodDictionaryName = "PIPEPLANDE_PARAMS";

    public static PipePlanDEParameters? GetEffective(Database db, int dn)
    {
        if (TryReadOverride(db, dn, out PipePlanDEParameters? overrideValue) && overrideValue is not null)
        {
            return overrideValue;
        }

        return PipePlanDEStandardTable.DefaultFor(dn);
    }

    public static void Set(Database db, int dn, PipePlanDEParameters parameters)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            DBDictionary store = GetOrCreateDictionary(nod, tx);
            string key = MakeKey(dn);
            ResultBuffer payload = Serialize(parameters);

            if (store.Contains(key))
            {
                Xrecord existing = (Xrecord)tx.GetObject(store.GetAt(key), OpenMode.ForWrite);
                existing.Data = payload;
            }
            else
            {
                Xrecord record = new() { Data = payload };
                store.SetAt(key, record);
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

    public static void ResetToDefault(Database db, int dn)
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

            DBDictionary store = (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForWrite);
            string key = MakeKey(dn);
            if (store.Contains(key))
            {
                DBObject child = tx.GetObject(store.GetAt(key), OpenMode.ForWrite);
                store.Remove(key);
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

    public static IReadOnlyList<PipePlanDEParameterEntry> EnumerateAll(Database db)
    {
        Dictionary<int, PipePlanDEParameters> overrides = ReadAllOverrides(db);
        List<PipePlanDEParameterEntry> entries = new();

        foreach (PipePlanDEStandardRow row in PipePlanDEStandardTable.Rows)
        {
            if (overrides.TryGetValue(row.Dn, out PipePlanDEParameters? overrideValue))
            {
                entries.Add(new PipePlanDEParameterEntry(row.Dn, row.Label, overrideValue, IsOverride: true));
            }
            else
            {
                entries.Add(new PipePlanDEParameterEntry(row.Dn, row.Label, row.Parameters, IsOverride: false));
            }
        }

        return entries;
    }

    private static bool TryReadOverride(Database db, int dn, out PipePlanDEParameters? parameters)
    {
        parameters = null;
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(NodDictionaryName))
            {
                tx.Commit();
                return false;
            }

            DBDictionary store = (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForRead);
            string key = MakeKey(dn);
            if (!store.Contains(key))
            {
                tx.Commit();
                return false;
            }

            Xrecord record = (Xrecord)tx.GetObject(store.GetAt(key), OpenMode.ForRead);
            ResultBuffer? buffer = record.Data;
            tx.Commit();

            return TryDeserialize(buffer, out parameters);
        }
        catch
        {
            tx.Abort();
            throw;
        }
    }

    private static Dictionary<int, PipePlanDEParameters> ReadAllOverrides(Database db)
    {
        Dictionary<int, PipePlanDEParameters> result = new();
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(NodDictionaryName))
            {
                tx.Commit();
                return result;
            }

            DBDictionary store = (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in store)
            {
                if (!int.TryParse(entry.Key, out int dn))
                {
                    continue;
                }

                Xrecord record = (Xrecord)tx.GetObject(entry.Value, OpenMode.ForRead);
                if (TryDeserialize(record.Data, out PipePlanDEParameters? parameters) && parameters is not null)
                {
                    result[dn] = parameters;
                }
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

    private static ResultBuffer Serialize(PipePlanDEParameters parameters)
    {
        TypedValue[] values = new TypedValue[PipePlanDEParameters.ColumnCount];
        for (int i = 0; i < PipePlanDEParameters.ColumnCount; i++)
        {
            values[i] = new TypedValue((int)DxfCode.Real, parameters[i]);
        }

        return new ResultBuffer(values);
    }

    private static bool TryDeserialize(ResultBuffer? buffer, out PipePlanDEParameters? parameters)
    {
        parameters = null;
        if (buffer is null)
        {
            return false;
        }

        TypedValue[] values = buffer.AsArray();
        if (values.Length != PipePlanDEParameters.ColumnCount)
        {
            return false;
        }

        double[] doubles = new double[PipePlanDEParameters.ColumnCount];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].Value is not double value)
            {
                return false;
            }

            doubles[i] = value;
        }

        PipePlanDEParameters candidate = new(doubles);
        // Ignore corrupt/invalid overrides so callers fall back to the standard table
        // rather than feeding bad geometry into PDDRAW/PDTRENCH.
        if (!candidate.TryValidate(out _))
        {
            return false;
        }

        parameters = candidate;
        return true;
    }

    private static DBDictionary GetOrCreateDictionary(DBDictionary nod, Transaction tx)
    {
        if (nod.Contains(NodDictionaryName))
        {
            return (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForWrite);
        }

        nod.UpgradeOpen();
        DBDictionary store = new();
        nod.SetAt(NodDictionaryName, store);
        tx.AddNewlyCreatedDBObject(store, add: true);
        return store;
    }

    private static string MakeKey(int dn) => dn.ToString();
}
