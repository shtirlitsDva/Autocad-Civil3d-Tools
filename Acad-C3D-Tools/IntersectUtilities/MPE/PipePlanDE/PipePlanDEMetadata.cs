using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.MPE.PipePlanDE;

internal enum PipePlanDERole
{
    Centerline = 0,
    Supply = 1,
    Return = 2,
}

/// <summary>
/// Which Regelgrabenbreite the trench uses, chosen by excavation depth. Shallow
/// (≤ 1.3 m) uses B, the common case; Deep (&gt; 1.3 m) uses the wider B1 (battered walls).
/// </summary>
internal enum PipePlanDETrenchDepth
{
    Shallow = 0, // ≤ 1.3 m → B
    Deep = 1,    // > 1.3 m → B1
}

internal sealed record PipePlanDEStoredData(
    int Dn,
    PipePlanDERole Role,
    PipePlanDETrenchDepth Depth = PipePlanDETrenchDepth.Shallow);

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
            new TypedValue((int)DxfCode.Int32, (int)data.Role),
            new TypedValue((int)DxfCode.Int32, (int)data.Depth));

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

        // Depth was added after V1 shipped; pre-depth records (length 3) default to
        // Shallow (B), which is the common case and the old behaviour.
        PipePlanDETrenchDepth depth = PipePlanDETrenchDepth.Shallow;
        if (values.Length >= 4 && values[3].Value is int depthValue
            && Enum.IsDefined(typeof(PipePlanDETrenchDepth), depthValue))
        {
            depth = (PipePlanDETrenchDepth)depthValue;
        }

        data = new PipePlanDEStoredData(dn, (PipePlanDERole)roleValue, depth);
        return true;
    }
}
