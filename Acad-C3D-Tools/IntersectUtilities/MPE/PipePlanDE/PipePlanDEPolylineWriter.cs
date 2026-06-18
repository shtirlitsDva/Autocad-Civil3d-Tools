using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>
/// Bakes a German pipe run as real entities: the routing centreline on
/// <see cref="CenterlineLayer"/> plus two mantle-OD-wide frem/retur polylines
/// mitered around it, each tagged with DN metadata for the later PDTRENCH command.
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
        int dn,
        PipePlanDEParameters parameters,
        bool flip,
        PipePlanDETrenchDepth depth,
        out string error)
    {
        error = string.Empty;

        if (controlPoints.Count < 2)
        {
            error = "Mindst to punkter kræves.";
            return false;
        }

        if (!PipePlanDEOffsetBuilder.TryBuild(controlPoints, parameters.PipeSpacing, out List<Point3d> left, out List<Point3d> right, out error))
        {
            return false;
        }

        // Flip swaps which physical side is frem (supply) vs retur.
        (List<Point3d> fremPoints, List<Point3d> returPoints) = flip ? (right, left) : (left, right);

        string fremLayer = SupplyLayer(dn);
        string returLayer = ReturnLayer(dn);

        db.CheckOrCreateLayer(CenterlineLayer);
        EnsureLayer(db, transaction, fremLayer, SupplyColorIndex, SupplyLinetype);
        EnsureLayer(db, transaction, returLayer, ReturnColorIndex, ReturnLinetype);

        BlockTableRecord modelSpace = db.GetModelspaceForWrite();

        Append(modelSpace, transaction, BuildPolyline(controlPoints, 0.0, CenterlineLayer), dn, PipePlanDERole.Centerline, depth);
        Append(modelSpace, transaction, BuildPolyline(fremPoints, parameters.D, fremLayer), dn, PipePlanDERole.Supply, depth);
        Append(modelSpace, transaction, BuildPolyline(returPoints, parameters.D, returLayer), dn, PipePlanDERole.Return, depth);

        return true;
    }

    private static void Append(BlockTableRecord modelSpace, Transaction transaction, Polyline polyline, int dn, PipePlanDERole role, PipePlanDETrenchDepth depth)
    {
        modelSpace.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, add: true);
        PipePlanDEMetadata.Write(polyline, new PipePlanDEStoredData(dn, role, depth), transaction);
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

    private static Polyline BuildPolyline(IReadOnlyList<Point3d> points, double width, string layer)
    {
        Polyline polyline = new();
        for (int i = 0; i < points.Count; i++)
        {
            polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0.0, width, width);
        }

        polyline.Closed = false;
        polyline.Layer = layer;
        polyline.Elevation = points[0].Z;
        polyline.Normal = Vector3d.ZAxis;
        return polyline;
    }
}
