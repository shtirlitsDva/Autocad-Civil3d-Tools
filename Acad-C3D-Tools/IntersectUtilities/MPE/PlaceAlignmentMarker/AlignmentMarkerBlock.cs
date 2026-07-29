using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.MPE.PlaceAlignmentMarker;

/// <summary>
/// The X marker placed by PLACEALIGNMENTMARKER. The block definition is built in the drawing on
/// first use — no dependency on the shared symbol library — from two crossing polylines spanning a
/// <see cref="HalfSize"/>-radius square around the block origin, so the insertion point sits exactly
/// on the crossing.
/// </summary>
internal static class AlignmentMarkerBlock
{
    internal const string BlockName = "MPE_ALIGNMENT_MARKER";
    internal const string LayerName = "0-ALIGNMENT-MARKER";
    internal const short LayerColorIndex = 1;

    /// <summary>Half the marker's width/height in drawing units; the X spans 2 × this.</summary>
    private const double HalfSize = 0.5;

    /// <summary>
    /// Returns the marker block's <see cref="BlockTableRecord"/> id, creating the definition if the
    /// drawing does not have it yet. Requires an active top transaction.
    /// </summary>
    internal static Oid EnsureDefinition(Database db)
    {
        Transaction tx = db.TransactionManager.TopTransaction;
        BlockTable bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);

        if (bt.Has(BlockName))
        {
            return bt[BlockName];
        }

        using BlockTableRecord btr = new()
        {
            Name = BlockName,
            Origin = Point3d.Origin,
        };

        // Appended while the record is still non-database-resident: adding the record below brings
        // the geometry with it, so the legs need no AddNewlyCreatedDBObject of their own. They stay
        // on layer 0 with ByLayer colour so the block reference's layer controls the appearance.
        btr.AppendEntity(CreateLeg(-HalfSize, -HalfSize, HalfSize, HalfSize));
        btr.AppendEntity(CreateLeg(-HalfSize, HalfSize, HalfSize, -HalfSize));

        bt.UpgradeOpen();
        Oid btrId = bt.Add(btr);
        tx.AddNewlyCreatedDBObject(btr, true);
        return btrId;
    }

    private static Polyline CreateLeg(double x1, double y1, double x2, double y2)
    {
        Polyline leg = new(2);
        leg.AddVertexAt(0, new Point2d(x1, y1), 0.0, 0.0, 0.0);
        leg.AddVertexAt(1, new Point2d(x2, y2), 0.0, 0.0, 0.0);
        return leg;
    }
}
