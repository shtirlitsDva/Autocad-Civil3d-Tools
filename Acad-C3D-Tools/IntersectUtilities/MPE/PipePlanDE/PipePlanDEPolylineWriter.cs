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

    public static string SupplyLayer(int dn) => $"FJV-FREM-DN{dn}";

    public static string ReturnLayer(int dn) => $"FJV-RETUR-DN{dn}";

    public static bool TryWrite(
        Database db,
        Transaction transaction,
        IReadOnlyList<Point3d> controlPoints,
        int dn,
        PipePlanDEParameters parameters,
        bool flip,
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
        db.CheckOrCreateLayer(fremLayer);
        db.CheckOrCreateLayer(returLayer);

        BlockTableRecord modelSpace = db.GetModelspaceForWrite();

        Append(modelSpace, transaction, BuildPolyline(controlPoints, 0.0, CenterlineLayer), dn, PipePlanDERole.Centerline);
        Append(modelSpace, transaction, BuildPolyline(fremPoints, parameters.D, fremLayer), dn, PipePlanDERole.Supply);
        Append(modelSpace, transaction, BuildPolyline(returPoints, parameters.D, returLayer), dn, PipePlanDERole.Return);

        return true;
    }

    private static void Append(BlockTableRecord modelSpace, Transaction transaction, Polyline polyline, int dn, PipePlanDERole role)
    {
        modelSpace.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, add: true);
        PipePlanDEMetadata.Write(polyline, new PipePlanDEStoredData(dn, role), transaction);
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
