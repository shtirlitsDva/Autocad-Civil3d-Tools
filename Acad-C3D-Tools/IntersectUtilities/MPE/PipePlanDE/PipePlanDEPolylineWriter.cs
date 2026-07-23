using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.MPE.PipePlan;
using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Bakes a German pipe run as real entities: the routing centreline on
/// <see cref="CenterlineLayer"/> plus two mantle-OD-wide frem/retur polylines
/// filleted (elastic bending radius) parallel to it, each tagged with DN metadata
/// for the later PDTRENCH command.
/// </summary>
internal static class PipePlanDEPolylineWriter
{
    public const string CenterlineLayer = "0-Centerline";

    // Layer templates applied when a FJV layer is first created (existing layers are
    // left untouched). FREM = red/Continuous; RETUR = blue/"DGN Style 3".
    private const short SupplyColorIndex = 1;  // red
    private const short ReturnColorIndex = 5;  // blue
    private const string SupplyLinetype = "Continuous";
    private const string ReturnLinetype = "DGN Style 3";

    public static string SupplyLayer(int dn) => $"FJV-FREM-DN{dn}";

    public static string ReturnLayer(int dn) => $"FJV-RETUR-DN{dn}";

    public static bool TryWrite(
        Database db,
        Transaction transaction,
        IReadOnlyList<Point3d> controlPoints,
        IReadOnlyList<double> rMinRadii,
        int dn,
        PipePlanDEParameters parameters,
        bool flip,
        bool straight,
        PipePlanDETrenchDepth depth,
        string? tokenOverride,
        out string token,
        out ObjectId[] createdIds,
        out string error)
    {
        error = string.Empty;
        token = string.Empty;
        createdIds = [];

        if (controlPoints.Count < 2)
        {
            error = "Mindst to punkter kræves.";
            return false;
        }

        if (!PipePlanDEGeometryBuilder.TryBuild(
                controlPoints, rMinRadii, parameters, flip, straight,
                out List<PolylineVertexData> centre,
                out List<PolylineVertexData> fremPoints,
                out List<PolylineVertexData> returPoints,
                out _,
                out error))
        {
            return false;
        }

        string fremLayer = SupplyLayer(dn);
        string returLayer = ReturnLayer(dn);

        db.CheckOrCreateLayer(CenterlineLayer);
        EnsureLayer(db, transaction, fremLayer, SupplyColorIndex, SupplyLinetype);
        EnsureLayer(db, transaction, returLayer, ReturnColorIndex, ReturnLinetype);

        BlockTableRecord modelSpace = db.GetModelspaceForWrite();
        double elevation = controlPoints[0].Z;

        // One token shared by the three polylines so PDEDIT can re-group them as one run.
        token = string.IsNullOrEmpty(tokenOverride) ? Guid.NewGuid().ToString("N") : tokenOverride!;

        // The centreline carries the authoring block; the bands carry only {token,dn,role,depth}.
        // Straight runs still store the modes but the radii are the (unused) endpoint-0 array.
        PipePlanDEAuthoring authoring = new(straight, flip, [.. controlPoints], [.. rMinRadii]);

        ObjectId centreId = Append(modelSpace, transaction, BuildPolyline(centre, 0.0, CenterlineLayer, elevation),
            new PipePlanDEStoredData(dn, PipePlanDERole.Centerline, depth, token, authoring));
        ObjectId fremId = Append(modelSpace, transaction, BuildPolyline(fremPoints, parameters.D, fremLayer, elevation),
            new PipePlanDEStoredData(dn, PipePlanDERole.Supply, depth, token));
        ObjectId returId = Append(modelSpace, transaction, BuildPolyline(returPoints, parameters.D, returLayer, elevation),
            new PipePlanDEStoredData(dn, PipePlanDERole.Return, depth, token));

        createdIds = [centreId, fremId, returId];
        return true;
    }

    private static ObjectId Append(BlockTableRecord modelSpace, Transaction transaction, Polyline polyline, PipePlanDEStoredData data)
    {
        modelSpace.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, add: true);
        PipePlanDEMetadata.Write(polyline, data, transaction);
        return polyline.ObjectId;
    }

    /// <summary>
    /// Creates <paramref name="name"/> with the given ACI colour and linetype if it does
    /// not exist. An EXISTING layer is left exactly as the user configured it — we only
    /// apply the template on first creation.
    /// </summary>
    private static void EnsureLayer(Database db, Transaction transaction, string name, short colorIndex, string linetypeName)
    {
        LayerTable layerTable = (LayerTable)transaction.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (layerTable.Has(name))
        {
            return;
        }

        layerTable.UpgradeOpen();
        LayerTableRecord record = new()
        {
            Name = name,
            IsPlottable = true,
            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
            LinetypeObjectId = ResolveLinetype(db, transaction, linetypeName),
            // LineWeight is left at its default (ByLineWeight → "Default" in the UI).
        };

        layerTable.Add(record);
        transaction.AddNewlyCreatedDBObject(record, add: true);
    }

    /// <summary>
    /// Resolves a linetype by name, falling back to Continuous when it isn't loaded.
    /// "DGN Style 3" is a DGN-import style that lives in the project template drawings,
    /// not in acad.lin — so when it's present we use it, and a blank drawing degrades to
    /// Continuous rather than failing the draw.
    /// </summary>
    private static ObjectId ResolveLinetype(Database db, Transaction transaction, string linetypeName)
    {
        LinetypeTable linetypeTable = (LinetypeTable)transaction.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        return linetypeTable.Has(linetypeName) ? linetypeTable[linetypeName] : db.ContinuousLinetype;
    }

    private static Polyline BuildPolyline(IReadOnlyList<PolylineVertexData> vertices, double width, string layer, double elevation)
    {
        Polyline polyline = new();
        for (int i = 0; i < vertices.Count; i++)
        {
            polyline.AddVertexAt(i, vertices[i].Point, vertices[i].Bulge, width, width);
        }

        polyline.Closed = false;
        polyline.Layer = layer;
        polyline.Elevation = elevation;
        polyline.Normal = Vector3d.ZAxis;
        return polyline;
    }
}
