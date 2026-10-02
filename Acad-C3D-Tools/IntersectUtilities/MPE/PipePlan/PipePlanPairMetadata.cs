using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>
/// Bonded-pair metadata, stored in each member's extension dictionary under
/// <c>pipePairData</c>. Deliberately a different key from the single-pipe
/// <c>pipeGeometryData</c>: a pair member must never read as a single PipePlan pipe,
/// or PPEDIT/Tangent would treat one pipe of a pair on its own.
///
/// Layout (PIPEPLAN_PAIR_V1):
/// [version][runToken][role][system][dn][series][jacketOdMm][minXMm][flip][snapText]
/// [count][count × control point][count × inner radius]
/// </summary>
internal static class PipePlanPairMetadata
{
    private const string PairDataKey = "pipePairData";
    private const string VersionV1 = "PIPEPLAN_PAIR_V1";
    private const int HeaderLength = 11;

    public static bool HasPairData(Polyline polyline, Transaction transaction)
    {
        if (polyline.ExtensionDictionary == ObjectId.Null)
        {
            return false;
        }

        DBDictionary dictionary = (DBDictionary)transaction.GetObject(polyline.ExtensionDictionary, OpenMode.ForRead);
        return dictionary.Contains(PairDataKey);
    }

    public static void Write(Polyline polyline, PipePlanPairStoredData data, Transaction transaction)
    {
        if (polyline.ExtensionDictionary == ObjectId.Null)
        {
            polyline.CreateExtensionDictionary();
        }

        DBDictionary dictionary = (DBDictionary)transaction.GetObject(polyline.ExtensionDictionary, OpenMode.ForWrite);
        ResultBuffer payload = Serialize(data);
        if (dictionary.Contains(PairDataKey))
        {
            Xrecord existing = (Xrecord)transaction.GetObject(dictionary.GetAt(PairDataKey), OpenMode.ForWrite);
            existing.Data = payload;
            return;
        }

        Xrecord record = new() { Data = payload };
        dictionary.SetAt(PairDataKey, record);
        transaction.AddNewlyCreatedDBObject(record, add: true);
    }

    /// <summary>None when the polyline is not a pair member; a Fault inside Some when it
    /// claims to be one but the record is unreadable.</summary>
    public static Option<Result<PipePlanPairStoredData>> Read(Polyline polyline, Transaction transaction)
    {
        if (!HasPairData(polyline, transaction))
        {
            return Option<Result<PipePlanPairStoredData>>.Nothing;
        }

        DBDictionary dictionary = (DBDictionary)transaction.GetObject(polyline.ExtensionDictionary, OpenMode.ForRead);
        Xrecord record = (Xrecord)transaction.GetObject(dictionary.GetAt(PairDataKey), OpenMode.ForRead);
        TypedValue[] values = record.Data is ResultBuffer buffer ? buffer.AsArray() : [];
        return Option<Result<PipePlanPairStoredData>>.Of(Deserialize(values));
    }

    private static ResultBuffer Serialize(PipePlanPairStoredData data)
    {
        IReadOnlyList<Point3d> points = data.Authoring.ControlPoints;
        List<TypedValue> values =
        [
            new TypedValue((int)DxfCode.Text, VersionV1),
            new TypedValue((int)DxfCode.Text, data.RunToken),
            new TypedValue((int)DxfCode.Int32, (int)data.Role),
            new TypedValue((int)DxfCode.Int32, (int)data.System),
            new TypedValue((int)DxfCode.Int32, data.Dn),
            new TypedValue((int)DxfCode.Int32, (int)data.Spacing.Series),
            new TypedValue((int)DxfCode.Real, data.Spacing.JacketOdMm),
            new TypedValue((int)DxfCode.Real, data.Spacing.MinXMm),
            new TypedValue((int)DxfCode.Int32, data.Authoring.Flip ? 1 : 0),
            new TypedValue((int)DxfCode.Text, data.StraightSnapToleranceText),
            new TypedValue((int)DxfCode.Int32, points.Count),
        ];

        values.AddRange(points.Select(p => new TypedValue((int)DxfCode.XCoordinate, p)));
        values.AddRange(data.Authoring.InnerRadii.Select(r => new TypedValue((int)DxfCode.Real, r)));
        return new ResultBuffer(values.ToArray());
    }

    private static Result<PipePlanPairStoredData> Deserialize(TypedValue[] values)
    {
        const string corrupt = "Parrets PipePlan-data er ulæselige.";
        if (values.Length < HeaderLength || values[0].Value is not string version || version != VersionV1)
        {
            return Result<PipePlanPairStoredData>.Failure(corrupt);
        }

        if (values[1].Value is not string token || string.IsNullOrWhiteSpace(token) ||
            values[2].Value is not int role || !Enum.IsDefined(typeof(PipePlanPairRole), role) ||
            values[3].Value is not int system || !Enum.IsDefined(typeof(PipeSystemEnum), system) ||
            values[4].Value is not int dn || dn <= 0 ||
            values[5].Value is not int series || !Enum.IsDefined(typeof(PipeSeriesEnum), series) ||
            values[6].Value is not double jacketOdMm || jacketOdMm <= 0.0 ||
            values[7].Value is not double minXMm || minXMm <= 0.0 ||
            values[8].Value is not int flip ||
            values[9].Value is not string snapText || string.IsNullOrWhiteSpace(snapText) ||
            values[10].Value is not int count || count < 2 ||
            values.Length != HeaderLength + (2 * count))
        {
            return Result<PipePlanPairStoredData>.Failure(corrupt);
        }

        List<Point3d> points = new(count);
        List<double> radii = new(count);
        for (int i = 0; i < count; i++)
        {
            if (values[HeaderLength + i].Value is not Point3d point ||
                values[HeaderLength + count + i].Value is not double radius)
            {
                return Result<PipePlanPairStoredData>.Failure(corrupt);
            }

            points.Add(point);
            radii.Add(radius);
        }

        return Result<PipePlanPairStoredData>.Success(new PipePlanPairStoredData(
            token,
            (PipePlanPairRole)role,
            (PipeSystemEnum)system,
            dn,
            new PipePlanPairSpacing((PipeSeriesEnum)series, jacketOdMm, minXMm),
            snapText,
            new PipePlanPairAuthoring(points, radii, flip != 0)));
    }
}
