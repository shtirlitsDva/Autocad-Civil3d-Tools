using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanMetadata
{
    public const string PipeTagAppName = "pipeTag";

    private const string PipeGeometryDataKey = "pipeGeometryData";
    private const string LegacyPipeGeometryDataKey = "PIPEPLAN_DATA";
    private const string PipeGeometryDataVersionV1 = "PIPEPLAN_V1";
    private const string PipeGeometryDataVersionV2 = "PIPEPLAN_V2";
    private const string PipeGeometryDataVersionV3 = "PIPEPLAN_V3";
    private const string PipeGeometryDataVersionV4 = "PIPEPLAN_V4";
    private const string PipeGeometryDataVersionV5 = "PIPEPLAN_V5";

    public static void EnsurePipeTagApp(Database database, Transaction transaction)
    {
        RegAppTable regAppTable = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (regAppTable.Has(PipeTagAppName))
        {
            return;
        }

        regAppTable.UpgradeOpen();
        RegAppTableRecord regApp = new()
        {
            Name = PipeTagAppName
        };
        regAppTable.Add(regApp);
        transaction.AddNewlyCreatedDBObject(regApp, add: true);
    }

    public static ResultBuffer CreatePipeTag(PipeSystemEnum system, PipeTypeEnum type, int dn)
    {
        return new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, PipeTagAppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"{system} {type} DN{dn}"));
    }

    public static void Write(Polyline polyline, PipePlanStoredData data, Transaction transaction)
    {
        // XData is tagged with the "pipeTag" RegApp; set_XData throws eRegappIdNotFound
        // unless that app is present in the drawing's RegAppTable. A drawing that has
        // never had a PipePlan pipe baked into it (e.g. the PPCONVERT path on a fresh
        // file) won't have it, so register it here rather than relying on the caller.
        EnsurePipeTagApp(polyline.Database, transaction);
        polyline.XData = CreatePipeTag(data.System, data.Type, data.Dn);

        if (polyline.ExtensionDictionary == ObjectId.Null)
        {
            polyline.CreateExtensionDictionary();
        }

        DBDictionary pipeData = (DBDictionary)transaction.GetObject(polyline.ExtensionDictionary, OpenMode.ForWrite);
        ResultBuffer payload = SerializePipeGeometryData(data);
        UpsertGeometryRecord(pipeData, payload, transaction);
        RemoveLegacyGeometryRecord(pipeData, transaction);
    }

    public static bool TryRead(Polyline polyline, Transaction transaction, out PipePlanStoredData? data)
    {
        data = null;
        if (!TryGetGeometryRecord(polyline, transaction, out Xrecord? pipeGeometryData) || pipeGeometryData is null)
        {
            return false;
        }

        ResultBuffer? buffer = pipeGeometryData.Data;
        if (buffer is null)
        {
            return false;
        }

        TypedValue[] values = buffer.AsArray();
        return TryDeserializePipeGeometryData(values, out data);
    }

    private static void UpsertGeometryRecord(DBDictionary pipeData, ResultBuffer payload, Transaction transaction)
    {
        if (pipeData.Contains(PipeGeometryDataKey))
        {
            Xrecord existingPipeGeometryData = (Xrecord)transaction.GetObject(pipeData.GetAt(PipeGeometryDataKey), OpenMode.ForWrite);
            existingPipeGeometryData.Data = payload;
            return;
        }

        Xrecord pipeGeometryData = new()
        {
            Data = payload
        };

        pipeData.SetAt(PipeGeometryDataKey, pipeGeometryData);
        transaction.AddNewlyCreatedDBObject(pipeGeometryData, add: true);
    }

    private static void RemoveLegacyGeometryRecord(DBDictionary pipeData, Transaction transaction)
    {
        if (!pipeData.Contains(LegacyPipeGeometryDataKey))
        {
            return;
        }

        DBObject legacyPipeGeometryData = transaction.GetObject(pipeData.GetAt(LegacyPipeGeometryDataKey), OpenMode.ForWrite);
        pipeData.Remove(LegacyPipeGeometryDataKey);
        legacyPipeGeometryData.Erase();
    }

    private static bool TryGetGeometryRecord(Polyline polyline, Transaction transaction, out Xrecord? pipeGeometryData)
    {
        pipeGeometryData = null;
        if (polyline.ExtensionDictionary == ObjectId.Null)
        {
            return false;
        }

        DBDictionary pipeData = (DBDictionary)transaction.GetObject(polyline.ExtensionDictionary, OpenMode.ForRead);
        string? pipeGeometryDataKey = ResolvePipeGeometryDataKey(pipeData);
        if (pipeGeometryDataKey is null)
        {
            return false;
        }

        pipeGeometryData = (Xrecord)transaction.GetObject(pipeData.GetAt(pipeGeometryDataKey), OpenMode.ForRead);
        return true;
    }

    private static ResultBuffer SerializePipeGeometryData(PipePlanStoredData data)
    {
        List<TypedValue> values =
        [
            new TypedValue((int)DxfCode.Text, PipeGeometryDataVersionV5),
            new TypedValue((int)DxfCode.Text, data.ObjectToken),
            new TypedValue((int)DxfCode.Int32, (int)data.System),
            new TypedValue((int)DxfCode.Int32, (int)data.Type),
            new TypedValue((int)DxfCode.Int32, data.Dn),
            new TypedValue((int)DxfCode.Text, data.StraightSnapToleranceText),
            new TypedValue((int)DxfCode.Int32, data.ControlPoints.Count)
        ];

        values.AddRange(data.ControlPoints.Select(point => new TypedValue((int)DxfCode.XCoordinate, point)));
        values.AddRange(data.BendRadii.Select(r => new TypedValue((int)DxfCode.Real, r)));

        // V5 trailing block: optional authoritative baked geometry. A count of 0 (the
        // only case until the arc-arc solver lands) means "no override — derive from
        // control points", so a plain fillet pipe's V5 record is V4 + a single 0.
        IReadOnlyList<PolylineVertexData> baked = data.BakedGeometry ?? [];
        values.Add(new TypedValue((int)DxfCode.Int32, baked.Count));
        values.AddRange(baked.Select(v => new TypedValue((int)DxfCode.XCoordinate, new Point3d(v.Point.X, v.Point.Y, 0.0))));
        values.AddRange(baked.Select(v => new TypedValue((int)DxfCode.Real, v.Bulge)));
        return new ResultBuffer(values.ToArray());
    }

    private static bool TryDeserializePipeGeometryData(TypedValue[] values, out PipePlanStoredData? data)
    {
        data = null;
        if (values.Length < 5)
        {
            return false;
        }

        string? versionToken = values[0].Value as string;
        return versionToken switch
        {
            PipeGeometryDataVersionV5 => TryReadV5(values, out data),
            PipeGeometryDataVersionV4 => TryReadV4(values, out data),
            PipeGeometryDataVersionV3 => TryReadV3(values, out data),
            PipeGeometryDataVersionV2 => TryReadV2(values, out data),
            PipeGeometryDataVersionV1 => TryReadV1(values, out data),
            _ => false
        };
    }

    private static bool TryReadV5(TypedValue[] values, out PipePlanStoredData? data)
    {
        data = null;
        if (values.Length < 7) return false;

        string? objectToken = values[1].Value as string;
        if (string.IsNullOrWhiteSpace(objectToken)) return false;
        if (values[2].Value is not int systemValue) return false;
        if (values[3].Value is not int typeValue) return false;
        if (values[4].Value is not int dn || dn <= 0) return false;
        string? snapToleranceText = values[5].Value as string;
        if (string.IsNullOrWhiteSpace(snapToleranceText)) return false;

        if (!TryReadPointCount(values[6], 7, values.Length - 7, out int pointCount)) return false;

        int radiiStart = 7 + pointCount;
        int bakedCountIndex = radiiStart + pointCount;
        // The V5 record always carries the baked-count marker after the radii, even
        // when it is 0; anything shorter is a truncated/corrupt payload.
        if (values.Length <= bakedCountIndex) return false;

        if (!TryReadControlPoints(values, 7, pointCount, out List<Point3d>? controlPoints) || controlPoints is null) return false;

        List<double> radii = new(pointCount);
        for (int i = 0; i < pointCount; i++)
        {
            if (values[radiiStart + i].Value is not double r) return false;
            radii.Add(r);
        }

        if (values[bakedCountIndex].Value is not int bakedCount || bakedCount < 0) return false;
        int bakedVerticesStart = bakedCountIndex + 1;
        int bakedBulgesStart = bakedVerticesStart + bakedCount;
        if (values.Length != bakedBulgesStart + bakedCount) return false;

        List<PolylineVertexData>? bakedGeometry = null;
        if (bakedCount > 0)
        {
            bakedGeometry = new List<PolylineVertexData>(bakedCount);
            for (int i = 0; i < bakedCount; i++)
            {
                if (values[bakedVerticesStart + i].Value is not Point3d vertex) return false;
                if (values[bakedBulgesStart + i].Value is not double bulge) return false;
                bakedGeometry.Add(new PolylineVertexData(new Point2d(vertex.X, vertex.Y), bulge));
            }
        }

        data = new PipePlanStoredData(
            (PipeSystemEnum)systemValue,
            (PipeTypeEnum)typeValue,
            dn,
            radii,
            snapToleranceText!,
            controlPoints,
            objectToken,
            bakedGeometry);
        return true;
    }

    private static bool TryReadV4(TypedValue[] values, out PipePlanStoredData? data)
    {
        data = null;
        if (values.Length < 7) return false;

        string? objectToken = values[1].Value as string;
        if (string.IsNullOrWhiteSpace(objectToken)) return false;
        if (values[2].Value is not int systemValue) return false;
        if (values[3].Value is not int typeValue) return false;
        if (values[4].Value is not int dn || dn <= 0) return false;
        string? snapToleranceText = values[5].Value as string;
        if (string.IsNullOrWhiteSpace(snapToleranceText)) return false;

        if (!TryReadPointCount(values[6], 7, values.Length - 7, out int pointCount)) return false;

        int radiiStart = 7 + pointCount;
        if (values.Length != radiiStart + pointCount) return false;

        if (!TryReadControlPoints(values, 7, pointCount, out List<Point3d>? controlPoints) || controlPoints is null) return false;

        List<double> radii = new(pointCount);
        for (int i = 0; i < pointCount; i++)
        {
            if (values[radiiStart + i].Value is not double r) return false;
            radii.Add(r);
        }

        data = new PipePlanStoredData(
            (PipeSystemEnum)systemValue,
            (PipeTypeEnum)typeValue,
            dn,
            radii,
            snapToleranceText!,
            controlPoints,
            objectToken);
        return true;
    }

    private static bool TryReadV3(TypedValue[] values, out PipePlanStoredData? data)
    {
        data = null;
        if (values.Length < 8) return false;

        string? objectToken = values[1].Value as string;
        if (string.IsNullOrWhiteSpace(objectToken)) return false;
        if (values[2].Value is not int systemValue) return false;
        if (values[3].Value is not int typeValue) return false;
        if (values[4].Value is not int dn || dn <= 0) return false;
        if (values[5].Value is not double radius || radius <= 0.0) return false;
        string? snapToleranceText = values[6].Value as string;
        if (string.IsNullOrWhiteSpace(snapToleranceText)) return false;

        if (!TryReadPointCount(values[7], 8, values.Length - 8, out int pointCount)) return false;
        if (!TryReadControlPoints(values, 8, pointCount, out List<Point3d>? controlPoints) || controlPoints is null) return false;
        if (values.Length != 8 + pointCount) return false;

        data = new PipePlanStoredData(
            (PipeSystemEnum)systemValue,
            (PipeTypeEnum)typeValue,
            dn,
            PipePlanStoredData.CreateUniformRadii(controlPoints.Count, radius),
            snapToleranceText!,
            controlPoints,
            objectToken);
        return true;
    }

    private static bool TryReadV2(TypedValue[] values, out PipePlanStoredData? data)
    {
        data = null;
        if (values.Length < 6) return false;

        string? objectToken = values[1].Value as string;
        string? sizeName = values[2].Value as string;
        string? radiusText = values[3].Value as string;
        string? snapToleranceText = values[4].Value as string;
        if (!TryValidateLegacyHeader(sizeName, radiusText, snapToleranceText)) return false;

        if (!TryReadPointCount(values[5], 6, values.Length - 6, out int pointCount)) return false;
        if (!TryReadControlPoints(values, 6, pointCount, out List<Point3d>? controlPoints) || controlPoints is null) return false;
        if (values.Length != 6 + pointCount) return false;

        if (!TrySynthesizeFromLegacy(sizeName!, radiusText!, out int dn, out double radius)) return false;

        data = new PipePlanStoredData(
            PipeSystemEnum.Stål,
            PipeTypeEnum.Twin,
            dn,
            PipePlanStoredData.CreateUniformRadii(controlPoints.Count, radius),
            snapToleranceText!,
            controlPoints,
            objectToken);
        return true;
    }

    private static bool TryReadV1(TypedValue[] values, out PipePlanStoredData? data)
    {
        data = null;
        if (values.Length < 5) return false;

        string? sizeName = values[1].Value as string;
        string? radiusText = values[2].Value as string;
        string? snapToleranceText = values[3].Value as string;
        if (!TryValidateLegacyHeader(sizeName, radiusText, snapToleranceText)) return false;

        if (!TryReadPointCount(values[4], 5, values.Length - 5, out int pointCount)) return false;
        if (!TryReadControlPoints(values, 5, pointCount, out List<Point3d>? controlPoints) || controlPoints is null) return false;
        if (values.Length != 5 + pointCount) return false;

        if (!TrySynthesizeFromLegacy(sizeName!, radiusText!, out int dn, out double radius)) return false;

        data = new PipePlanStoredData(
            PipeSystemEnum.Stål,
            PipeTypeEnum.Twin,
            dn,
            PipePlanStoredData.CreateUniformRadii(controlPoints.Count, radius),
            snapToleranceText!,
            controlPoints);
        return true;
    }

    private static bool TrySynthesizeFromLegacy(string sizeName, string radiusText, out int dn, out double radius)
    {
        dn = 0;
        radius = 0.0;
        string digits = new(sizeName.Where(char.IsDigit).ToArray());
        if (!int.TryParse(digits, out dn) || dn <= 0) return false;
        if (!PipePlanParsing.TryParsePositiveDouble(radiusText, out radius) || radius <= 0.0) return false;
        return true;
    }

    private static bool TryValidateLegacyHeader(string? sizeName, string? radiusText, string? snapToleranceText)
    {
        return !string.IsNullOrWhiteSpace(sizeName) &&
               !string.IsNullOrWhiteSpace(radiusText) &&
               !string.IsNullOrWhiteSpace(snapToleranceText);
    }

    private static bool TryReadPointCount(TypedValue value, int startIndex, int remainingValueCount, out int pointCount)
    {
        pointCount = 0;
        if (value.Value is not int parsedPointCount || parsedPointCount < 2)
        {
            return false;
        }

        pointCount = parsedPointCount;
        return true;
    }

    private static bool TryReadControlPoints(TypedValue[] values, int startIndex, int pointCount, out List<Point3d>? controlPoints)
    {
        controlPoints = new List<Point3d>(pointCount);
        for (int index = 0; index < pointCount; index++)
        {
            if (values[startIndex + index].Value is not Point3d point)
            {
                controlPoints = null;
                return false;
            }

            controlPoints.Add(point);
        }

        return true;
    }

    private static string? ResolvePipeGeometryDataKey(DBDictionary pipeData)
    {
        if (pipeData.Contains(PipeGeometryDataKey))
        {
            return PipeGeometryDataKey;
        }

        return pipeData.Contains(LegacyPipeGeometryDataKey)
            ? LegacyPipeGeometryDataKey
            : null;
    }
}
