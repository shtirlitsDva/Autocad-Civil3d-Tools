using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed record PipePlanMinX(double Millimetres, PipePlanRadiusSource Source);

/// <summary>
/// The per-DN minimum jacket-to-jacket gap ("min x") of a bonded pair, in mm.
///
/// The default is NorsynDrawingTools' own default (TrenchProfile.h defaultMinX), so an
/// untouched drawing spaces pairs exactly as NDHPIPE does. Overrides live in this
/// drawing's NOD under <see cref="NodDictionaryName"/> — NOT in NDH's settings, which
/// are a closed binary ObjectARX object (see ppdraw-bonded-pairs.md).
/// </summary>
internal static class PipePlanPairGapStore
{
    private const string NodDictionaryName = "PIPEPLAN_PAIR_MINX";

    public static double DefaultMinXMm(int dn) => dn <= 150 ? 300.0 : dn <= 450 ? 400.0 : 450.0;

    public static PipePlanMinX Get(Database db, PipeSystemEnum system, int dn) =>
        ReadOverride(db, system, dn).Match(
            value => new PipePlanMinX(value, PipePlanRadiusSource.Override),
            () => new PipePlanMinX(DefaultMinXMm(dn), PipePlanRadiusSource.Default));

    public static void Set(Database db, PipeSystemEnum system, int dn, double millimetres)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        DBDictionary gaps = GetOrCreateDictionary(nod, tx);
        string key = MakeKey(system, dn);
        ResultBuffer payload = new(new TypedValue((int)DxfCode.Real, millimetres));

        if (gaps.Contains(key))
        {
            Xrecord existing = (Xrecord)tx.GetObject(gaps.GetAt(key), OpenMode.ForWrite);
            existing.Data = payload;
        }
        else
        {
            Xrecord record = new() { Data = payload };
            gaps.SetAt(key, record);
            tx.AddNewlyCreatedDBObject(record, add: true);
        }

        tx.Commit();
    }

    public static void ResetToDefault(Database db, PipeSystemEnum system, int dn)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (nod.Contains(NodDictionaryName))
        {
            DBDictionary gaps = (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForWrite);
            string key = MakeKey(system, dn);
            if (gaps.Contains(key))
            {
                DBObject child = tx.GetObject(gaps.GetAt(key), OpenMode.ForWrite);
                gaps.Remove(key);
                child.Erase();
            }
        }

        tx.Commit();
    }

    private static Option<double> ReadOverride(Database db, PipeSystemEnum system, int dn)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        Option<double> value = Option<double>.Nothing;
        if (nod.Contains(NodDictionaryName))
        {
            DBDictionary gaps = (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForRead);
            string key = MakeKey(system, dn);
            if (gaps.Contains(key))
            {
                Xrecord record = (Xrecord)tx.GetObject(gaps.GetAt(key), OpenMode.ForRead);
                TypedValue[] values = record.Data is ResultBuffer buffer ? buffer.AsArray() : [];
                if (values.Length > 0 && values[0].Value is double millimetres && millimetres > 0.0)
                {
                    value = Option<double>.Of(millimetres);
                }
            }
        }

        tx.Commit();
        return value;
    }

    private static DBDictionary GetOrCreateDictionary(DBDictionary nod, Transaction tx)
    {
        if (nod.Contains(NodDictionaryName))
        {
            return (DBDictionary)tx.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForWrite);
        }

        nod.UpgradeOpen();
        DBDictionary gaps = new();
        nod.SetAt(NodDictionaryName, gaps);
        tx.AddNewlyCreatedDBObject(gaps, add: true);
        return gaps;
    }

    private static string MakeKey(PipeSystemEnum system, int dn) => $"{system}.{PipeTypeEnum.Enkelt}.{dn}";
}
