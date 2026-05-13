using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace PipePlan.Plugin;

internal static class PipePlanMetadata
{
    public const string PipeTagAppName = "pipeTag";

    private const string PipeGeometryDataKey = "pipeGeometryData";
    private const string LegacyPipeGeometryDataKey = "PIPEPLAN_DATA";
    private const string PipeGeometryDataVersionV1 = "PIPEPLAN_V1";
    private const string PipeGeometryDataVersionV2 = "PIPEPLAN_V2";

    public static ResultBuffer CreatePipeTag(string sizeName, string radiusText)
    {
        return new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, PipeTagAppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, sizeName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, radiusText));
    }

    public static void Write(Polyline polyline, PipePlanStoredData data, Transaction transaction)
    {
        polyline.XData = CreatePipeTag(data.SizeName, data.RadiusText);

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
            new TypedValue((int)DxfCode.Text, PipeGeometryDataVersionV2),
            new TypedValue((int)DxfCode.Text, data.ObjectToken),
            new TypedValue((int)DxfCode.Text, data.SizeName),
            new TypedValue((int)DxfCode.Text, data.RadiusText),
            new TypedValue((int)DxfCode.Text, data.StraightSnapToleranceText),
            new TypedValue((int)DxfCode.Int32, data.ControlPoints.Count)
        ];

        values.AddRange(data.ControlPoints.Select(point => new TypedValue((int)DxfCode.XCoordinate, point)));
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
        if (string.Equals(versionToken, PipeGeometryDataVersionV2, StringComparison.Ordinal))
        {
            return TryReadV2(values, out data);
        }

        return string.Equals(versionToken, PipeGeometryDataVersionV1, StringComparison.Ordinal) &&
               TryReadV1(values, out data);
    }

    private static bool TryReadV2(TypedValue[] values, out PipePlanStoredData? data)
    {
        data = null;
        if (values.Length < 6)
        {
            return false;
        }

        string? objectToken = values[1].Value as string;
        string? sizeName = values[2].Value as string;
        string? radiusText = values[3].Value as string;
        string? snapToleranceText = values[4].Value as string;
        if (!TryValidateHeader(sizeName, radiusText, snapToleranceText))
        {
            return false;
        }

        if (!TryReadPointCount(values[5], 6, values.Length, out int pointCount))
        {
            return false;
        }

        if (!TryReadControlPoints(values, 6, pointCount, out List<Point3d>? controlPoints) || controlPoints is null)
        {
            return false;
        }

        data = new PipePlanStoredData(sizeName!, radiusText!, snapToleranceText!, controlPoints, objectToken);
        return true;
    }

    private static bool TryReadV1(TypedValue[] values, out PipePlanStoredData? data)
    {
        data = null;
        if (values.Length < 5)
        {
            return false;
        }

        string? sizeName = values[1].Value as string;
        string? radiusText = values[2].Value as string;
        string? snapToleranceText = values[3].Value as string;
        if (!TryValidateHeader(sizeName, radiusText, snapToleranceText))
        {
            return false;
        }

        if (!TryReadPointCount(values[4], 5, values.Length, out int pointCount))
        {
            return false;
        }

        if (!TryReadControlPoints(values, 5, pointCount, out List<Point3d>? controlPoints) || controlPoints is null)
        {
            return false;
        }

        data = new PipePlanStoredData(sizeName!, radiusText!, snapToleranceText!, controlPoints);
        return true;
    }

    private static bool TryValidateHeader(string? sizeName, string? radiusText, string? snapToleranceText)
    {
        return !string.IsNullOrWhiteSpace(sizeName) &&
               !string.IsNullOrWhiteSpace(radiusText) &&
               !string.IsNullOrWhiteSpace(snapToleranceText);
    }

    private static bool TryReadPointCount(TypedValue value, int startIndex, int valueCount, out int pointCount)
    {
        pointCount = 0;
        if (value.Value is not int parsedPointCount || parsedPointCount < 2)
        {
            return false;
        }

        if (valueCount != parsedPointCount + startIndex)
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
