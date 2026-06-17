using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.MPE.PipePlanDE;

internal enum PipePlanDERole
{
    Centerline = 0,
    Supply = 1,
    Return = 2,
}

internal sealed record PipePlanDEStoredData(int Dn, PipePlanDERole Role);

/// <summary>
/// Per-polyline German-pipe metadata, stored on the entity's ExtensionDictionary
/// under "pipeGeometryDataDE" (mirrors <c>PipePlanMetadata</c>). Carries the DN —
/// the "information stored on the polyline" that the later PDTRENCH command reads
/// to look up the trench widths — plus the role so each of the three baked
/// polylines is self-describing.
/// </summary>
internal static class PipePlanDEMetadata
{
    private const string GeometryDataKey = "pipeGeometryDataDE";
    private const string VersionV1 = "PIPEPLANDE_V1";

    public static void Write(Polyline polyline, PipePlanDEStoredData data, Transaction transaction)
    {
        if (polyline.ExtensionDictionary == ObjectId.Null)
        {
            polyline.CreateExtensionDictionary();
        }

        DBDictionary dict = (DBDictionary)transaction.GetObject(polyline.ExtensionDictionary, OpenMode.ForWrite);
        ResultBuffer payload = new(
            new TypedValue((int)DxfCode.Text, VersionV1),
            new TypedValue((int)DxfCode.Int32, data.Dn),
            new TypedValue((int)DxfCode.Int32, (int)data.Role));

        if (dict.Contains(GeometryDataKey))
        {
            Xrecord existing = (Xrecord)transaction.GetObject(dict.GetAt(GeometryDataKey), OpenMode.ForWrite);
            existing.Data = payload;
        }
        else
        {
            Xrecord record = new() { Data = payload };
            dict.SetAt(GeometryDataKey, record);
            transaction.AddNewlyCreatedDBObject(record, add: true);
        }
    }

    public static bool TryRead(Polyline polyline, Transaction transaction, out PipePlanDEStoredData? data)
    {
        data = null;
        if (polyline.ExtensionDictionary == ObjectId.Null)
        {
            return false;
        }

        DBDictionary dict = (DBDictionary)transaction.GetObject(polyline.ExtensionDictionary, OpenMode.ForRead);
        if (!dict.Contains(GeometryDataKey))
        {
            return false;
        }

        Xrecord record = (Xrecord)transaction.GetObject(dict.GetAt(GeometryDataKey), OpenMode.ForRead);
        TypedValue[]? values = record.Data?.AsArray();
        if (values is null || values.Length < 3)
        {
            return false;
        }

        if (values[0].Value as string != VersionV1)
        {
            return false;
        }

        if (values[1].Value is not int dn || dn <= 0)
        {
            return false;
        }

        if (values[2].Value is not int roleValue)
        {
            return false;
        }

        data = new PipePlanDEStoredData(dn, (PipePlanDERole)roleValue);
        return true;
    }
}
