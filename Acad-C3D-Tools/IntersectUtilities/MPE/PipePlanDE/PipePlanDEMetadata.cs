using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

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

/// <summary>
/// The authoring data needed to re-edit a run (PDEDIT): the routing control points, the
/// per-corner inner-pipe bending radii (0 at endpoints), and the flip/straight modes.
/// Stored only on the centreline polyline of a run (role == Centerline).
/// </summary>
internal sealed record PipePlanDEAuthoring(
    bool Straight,
    bool Flip,
    IReadOnlyList<Point3d> ControlPoints,
    IReadOnlyList<double> RMinRadii);

internal sealed record PipePlanDEStoredData(
    int Dn,
    PipePlanDERole Role,
    PipePlanDETrenchDepth Depth = PipePlanDETrenchDepth.Shallow,
    string? Token = null,
    PipePlanDEAuthoring? Authoring = null);

/// <summary>
/// Per-polyline German-pipe metadata, stored on the entity's ExtensionDictionary
/// under "pipeGeometryDataDE" (mirrors <c>PipePlanMetadata</c>).
///
/// V1 carried only {DN, Role, Depth}. V2 adds a shared run <c>Token</c> (GUID linking a
/// run's three polylines) and, on the centreline only, the authoring block (control
/// points + per-corner rMin radii + flip/straight) that PDEDIT re-solves from. V1 records
/// stay readable (Token/Authoring null) but such runs are not editable.
/// </summary>
internal static class PipePlanDEMetadata
{
    private const string GeometryDataKey = "pipeGeometryDataDE";
    private const string VersionV1 = "PIPEPLANDE_V1";
    private const string VersionV2 = "PIPEPLANDE_V2";

    public static void Write(Polyline polyline, PipePlanDEStoredData data, Transaction transaction)
    {
        if (polyline.ExtensionDictionary == ObjectId.Null)
        {
            polyline.CreateExtensionDictionary();
        }

        DBDictionary dict = (DBDictionary)transaction.GetObject(polyline.ExtensionDictionary, OpenMode.ForWrite);
        ResultBuffer payload = Serialize(data);

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

    private static ResultBuffer Serialize(PipePlanDEStoredData data)
    {
        List<TypedValue> values =
        [
            new TypedValue((int)DxfCode.Text, VersionV2),
            new TypedValue((int)DxfCode.Text, data.Token ?? string.Empty),
            new TypedValue((int)DxfCode.Int32, data.Dn),
            new TypedValue((int)DxfCode.Int32, (int)data.Role),
            new TypedValue((int)DxfCode.Int32, (int)data.Depth),
        ];

        // Authoring block is carried only by the centreline. Endpoints' radii are 0.
        if (data.Role == PipePlanDERole.Centerline && data.Authoring is PipePlanDEAuthoring authoring)
        {
            IReadOnlyList<Point3d> cps = authoring.ControlPoints;
            IReadOnlyList<double> radii = authoring.RMinRadii;
            values.Add(new TypedValue((int)DxfCode.Int32, authoring.Straight ? 1 : 0));
            values.Add(new TypedValue((int)DxfCode.Int32, authoring.Flip ? 1 : 0));
            values.Add(new TypedValue((int)DxfCode.Int32, cps.Count));
            values.AddRange(cps.Select(p => new TypedValue((int)DxfCode.XCoordinate, p)));
            values.AddRange(radii.Select(r => new TypedValue((int)DxfCode.Real, r)));
        }

        return new ResultBuffer(values.ToArray());
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

        return values[0].Value as string switch
        {
            VersionV2 => TryReadV2(values, out data),
            VersionV1 => TryReadV1(values, out data),
            _ => false,
        };
    }

    private static bool TryReadV1(TypedValue[] values, out PipePlanDEStoredData? data)
    {
        data = null;
        if (values.Length < 3)
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

        // Depth was added after V1 shipped; pre-depth records (length 3) default to Shallow.
        PipePlanDETrenchDepth depth = PipePlanDETrenchDepth.Shallow;
        if (values.Length >= 4 && values[3].Value is int depthValue
            && Enum.IsDefined(typeof(PipePlanDETrenchDepth), depthValue))
        {
            depth = (PipePlanDETrenchDepth)depthValue;
        }

        data = new PipePlanDEStoredData(dn, (PipePlanDERole)roleValue, depth);
        return true;
    }

    private static bool TryReadV2(TypedValue[] values, out PipePlanDEStoredData? data)
    {
        data = null;
        if (values.Length < 5)
        {
            return false;
        }

        string token = values[1].Value as string ?? string.Empty;
        if (values[2].Value is not int dn || dn <= 0)
        {
            return false;
        }

        if (values[3].Value is not int roleValue || !Enum.IsDefined(typeof(PipePlanDERole), roleValue))
        {
            return false;
        }

        if (values[4].Value is not int depthValue || !Enum.IsDefined(typeof(PipePlanDETrenchDepth), depthValue))
        {
            return false;
        }

        PipePlanDERole role = (PipePlanDERole)roleValue;
        PipePlanDETrenchDepth depth = (PipePlanDETrenchDepth)depthValue;

        PipePlanDEAuthoring? authoring = null;
        if (role == PipePlanDERole.Centerline && values.Length > 5)
        {
            if (!TryReadAuthoring(values, 5, out authoring))
            {
                return false;
            }
        }

        data = new PipePlanDEStoredData(dn, role, depth, string.IsNullOrEmpty(token) ? null : token, authoring);
        return true;
    }

    private static bool TryReadAuthoring(TypedValue[] values, int start, out PipePlanDEAuthoring? authoring)
    {
        authoring = null;
        // [straight][flip][cpCount][cpCount×Point3d][cpCount×Real]
        if (values.Length < start + 3)
        {
            return false;
        }

        if (values[start].Value is not int straightValue) return false;
        if (values[start + 1].Value is not int flipValue) return false;
        if (values[start + 2].Value is not int cpCount || cpCount < 2) return false;

        int cpStart = start + 3;
        int radiiStart = cpStart + cpCount;
        if (values.Length != radiiStart + cpCount)
        {
            return false;
        }

        List<Point3d> controlPoints = new(cpCount);
        for (int i = 0; i < cpCount; i++)
        {
            if (values[cpStart + i].Value is not Point3d p) return false;
            controlPoints.Add(p);
        }

        List<double> radii = new(cpCount);
        for (int i = 0; i < cpCount; i++)
        {
            if (values[radiiStart + i].Value is not double r) return false;
            radii.Add(r);
        }

        authoring = new PipePlanDEAuthoring(straightValue != 0, flipValue != 0, controlPoints, radii);
        return true;
    }
}
