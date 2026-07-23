using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>The PDANNOTATE settings persisted per drawing: the dimension-style name (empty =
/// the drawing's current DIMSTYLE) and the dimension-line offset from the centreline.</summary>
internal readonly record struct PipePlanDEAnnotationSettings(string StyleName, double Offset);

/// <summary>
/// Per-drawing PDANNOTATE settings, stored in the Named Objects Dictionary under
/// "PIPEPLANDE_ANNO" as a single Xrecord ([Text styleName][Real offset]). Mirrors the NOD
/// storage idiom of <see cref="PipePlanDEParameterStore"/>. A missing/corrupt record yields
/// the defaults (current DIMSTYLE, offset 3 m).
/// </summary>
internal static class PipePlanDEAnnotationStore
{
    private const string NodDictionaryName = "PIPEPLANDE_ANNO";
    private const string RecordKey = "settings";
    private const double DefaultOffset = 3.0;

    public static PipePlanDEAnnotationSettings Get(Database db)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (nod.Contains(NodDictionaryName))
            {
                DBDictionary store = (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForRead);
                if (store.Contains(RecordKey))
                {
                    Xrecord record = (Xrecord)tx.GetObject(store.GetAt(RecordKey), OpenMode.ForRead);
                    if (TryDeserialize(record.Data, out PipePlanDEAnnotationSettings settings))
                    {
                        tx.Commit();
                        return settings;
                    }
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Abort();
            throw;
        }

        return new PipePlanDEAnnotationSettings(string.Empty, DefaultOffset);
    }

    public static void Set(Database db, PipePlanDEAnnotationSettings settings)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            DBDictionary store = GetOrCreateDictionary(nod, tx);

            ResultBuffer payload = new(
                new TypedValue((int)DxfCode.Text, settings.StyleName ?? string.Empty),
                new TypedValue((int)DxfCode.Real, settings.Offset));

            if (store.Contains(RecordKey))
            {
                Xrecord existing = (Xrecord)tx.GetObject(store.GetAt(RecordKey), OpenMode.ForWrite);
                existing.Data = payload;
            }
            else
            {
                Xrecord record = new() { Data = payload };
                store.SetAt(RecordKey, record);
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

    private static bool TryDeserialize(ResultBuffer? buffer, out PipePlanDEAnnotationSettings settings)
    {
        settings = default;
        if (buffer is null)
        {
            return false;
        }

        TypedValue[] values = buffer.AsArray();
        if (values.Length < 2 || values[0].Value is not string styleName || values[1].Value is not double offset)
        {
            return false;
        }

        settings = new PipePlanDEAnnotationSettings(styleName, offset > 0.0 ? offset : DefaultOffset);
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
}
