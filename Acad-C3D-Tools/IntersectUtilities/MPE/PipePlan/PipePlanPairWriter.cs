using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

/// <summary>
/// Writes a bonded run: the centreline on <see cref="CentrelineLayer"/> and frem/retur on
/// their FJV layers, all three carrying the same pair metadata. Members that already
/// exist are reshaped in place (Handle, ObjectId and third-party data survive); an empty
/// role gets a new polyline. The caller owns the transaction, so a run is one undo step.
/// </summary>
internal static class PipePlanPairWriter
{
    /// <summary>Not 0-FJV-CL: DRAWFJVCL erases every polyline on that layer.</summary>
    public const string CentrelineLayer = "0-FJV-PP-CL";

    private const short CentrelineColorIndex = 6;
    private const string CentrelineLinetype = "DASHED";

    public static string LayerFor(PipePlanPairRole role, PipeSystemEnum system, int dn) => role switch
    {
        PipePlanPairRole.Centerline => CentrelineLayer,
        PipePlanPairRole.Frem => PipeScheduleV2.PipeScheduleV2.GetLayerName(dn, system, PipeTypeEnum.Frem),
        PipePlanPairRole.Retur => PipeScheduleV2.PipeScheduleV2.GetLayerName(dn, system, PipeTypeEnum.Retur),
    };

    public static PipePlanPairSlots Write(
        Database db,
        Transaction tx,
        PipePlanPairSlots slots,
        PipePlanPairSolution solution,
        PipePlanPairStoredData data)
    {
        EnsureCentrelineLayer(db, tx);
        EnsurePipeLayer(db, tx, data.System, PipeTypeEnum.Frem, data.Dn);
        EnsurePipeLayer(db, tx, data.System, PipeTypeEnum.Retur, data.Dn);
        PipePlanMetadata.EnsurePipeTagApp(db, tx);

        double elevation = data.Authoring.ControlPoints[0].Z;
        PipePlanPairSlots written = slots;
        foreach (PipePlanPairRole role in PipePlanPairSlots.Roles)
        {
            IReadOnlyList<PolylineVertexData> vertices = solution.VerticesFor(role);
            double width = role == PipePlanPairRole.Centerline ? 0.0 : data.Spacing.PipeWidth;
            string layer = LayerFor(role, data.System, data.Dn);

            Polyline polyline = slots.For(role).Match(
                id => (Polyline)tx.GetObject(id, OpenMode.ForWrite),
                () => AppendNew(db, tx));
            Reshape(polyline, vertices, width, layer, elevation);
            PipePlanPairMetadata.Write(polyline, data.ForRole(role), tx);
            if (role != PipePlanPairRole.Centerline)
            {
                PipeTypeEnum type = role == PipePlanPairRole.Frem ? PipeTypeEnum.Frem : PipeTypeEnum.Retur;
                polyline.XData = PipePlanMetadata.CreatePipeTag(data.System, type, data.Dn);
            }

            written = written.With(role, Option<ObjectId>.Of(polyline.ObjectId));
        }

        return written;
    }

    private static Polyline AppendNew(Database db, Transaction tx)
    {
        Polyline polyline = new();
        polyline.SetDatabaseDefaults(db);
        BlockTable blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        // A polyline needs a vertex before it can be appended.
        polyline.AddVertexAt(0, Point2d.Origin, 0.0, 0.0, 0.0);
        modelSpace.AppendEntity(polyline);
        tx.AddNewlyCreatedDBObject(polyline, add: true);
        return polyline;
    }

    private static void Reshape(Polyline target, IReadOnlyList<PolylineVertexData> vertices, double width, string layer, double elevation)
    {
        int oldCount = target.NumberOfVertices;
        int newCount = vertices.Count;
        int overlap = Math.Min(oldCount, newCount);

        for (int i = 0; i < overlap; i++)
        {
            target.SetPointAt(i, vertices[i].Point);
            target.SetBulgeAt(i, vertices[i].Bulge);
            target.SetStartWidthAt(i, 0.0);
            target.SetEndWidthAt(i, 0.0);
        }

        for (int j = oldCount; j < newCount; j++)
        {
            target.AddVertexAt(j, vertices[j].Point, vertices[j].Bulge, 0.0, 0.0);
        }

        // Remove from the tail downward: RemoveVertexAt shifts the trailing indices.
        for (int j = oldCount - 1; j >= newCount; j--)
        {
            target.RemoveVertexAt(j);
        }

        target.Layer = layer;
        target.Elevation = elevation;
        target.Normal = Vector3d.ZAxis;
        target.ConstantWidth = width;
        target.Closed = false;
    }

    private static void EnsureCentrelineLayer(Database db, Transaction tx)
    {
        LayerTable layerTable = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (layerTable.Has(CentrelineLayer))
        {
            return;
        }

        LayerTableRecord layer = new()
        {
            Name = CentrelineLayer,
            Color = Color.FromColorIndex(ColorMethod.ByAci, CentrelineColorIndex),
            IsPlottable = false,
            LinetypeObjectId = ResolveDashedLinetype(db, tx),
        };
        layerTable.UpgradeOpen();
        layerTable.Add(layer);
        tx.AddNewlyCreatedDBObject(layer, add: true);
    }

    /// <summary>The way NSPalette creates an FJV layer: its system/type colour, Continuous.</summary>
    private static void EnsurePipeLayer(Database db, Transaction tx, PipeSystemEnum system, PipeTypeEnum type, int dn)
    {
        string name = PipeScheduleV2.PipeScheduleV2.GetLayerName(dn, system, type);
        LayerTable layerTable = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (layerTable.Has(name))
        {
            return;
        }

        LayerTableRecord layer = new()
        {
            Name = name,
            Color = Color.FromColorIndex(ColorMethod.ByAci, PipeScheduleV2.PipeScheduleV2.GetLayerColor(system, type)),
            LinetypeObjectId = db.ContinuousLinetype,
            LineWeight = LineWeight.ByLineWeightDefault,
        };
        layerTable.UpgradeOpen();
        layerTable.Add(layer);
        tx.AddNewlyCreatedDBObject(layer, add: true);
    }

    private static ObjectId ResolveDashedLinetype(Database db, Transaction tx)
    {
        LinetypeTable linetypes = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        if (linetypes.Has(CentrelineLinetype))
        {
            return linetypes[CentrelineLinetype];
        }

        try
        {
            db.LoadLineTypeFile(CentrelineLinetype, "acad.lin");
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            // Boundary to the AutoCAD API: no acad.lin on this machine. Continuous will do.
            return db.ContinuousLinetype;
        }

        linetypes = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        return linetypes.Has(CentrelineLinetype) ? linetypes[CentrelineLinetype] : db.ContinuousLinetype;
    }
}
