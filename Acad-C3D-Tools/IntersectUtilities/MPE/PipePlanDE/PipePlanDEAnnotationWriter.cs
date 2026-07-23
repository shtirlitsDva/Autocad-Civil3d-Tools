using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>One fillet arc to annotate with a DIMARC arc-length dimension.</summary>
internal readonly record struct PipePlanDEArcDim(Point3d Center, Point3d Start, Point3d End, Point3d Mid, double Radius);

/// <summary>
/// Bakes PDANNOTATE dimensions: an <see cref="AlignedDimension"/> per corner-to-corner (split)
/// span and an <see cref="ArcDimension"/> (DIMARC arc-length) per fillet arc, all on the
/// <see cref="AnnotationLayer"/> in the chosen dimension style. Text is auto-measured (empty
/// override). Follows the dimension-build idiom in FjernvarmeFremtidig.cs.
/// </summary>
internal static class PipePlanDEAnnotationWriter
{
    public const string AnnotationLayer = "PD-Anno";

    public static int Write(
        Database db,
        Transaction transaction,
        IReadOnlyList<(Point3d Start, Point3d End)> alignedSpans,
        IReadOnlyList<PipePlanDEArcDim> arcs,
        double offset,
        ObjectId dimStyleId,
        double elevation)
    {
        db.CheckOrCreateLayer(AnnotationLayer);
        BlockTableRecord modelSpace = db.GetModelspaceForWrite();
        int count = 0;

        foreach ((Point3d start, Point3d end) in alignedSpans)
        {
            if (start.DistanceTo(end) < 1e-6)
            {
                continue;
            }

            // Dimension line offset perpendicular (left of Start→End) by `offset`.
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double len = Math.Sqrt((dx * dx) + (dy * dy));
            double nx = -dy / len;
            double ny = dx / len;
            Point3d dimLine = new(
                ((start.X + end.X) / 2.0) + (nx * offset),
                ((start.Y + end.Y) / 2.0) + (ny * offset),
                elevation);

            AlignedDimension dim = new(start, end, dimLine, string.Empty, dimStyleId);
            Configure(dim, db, dimStyleId);
            modelSpace.AppendEntity(dim);
            transaction.AddNewlyCreatedDBObject(dim, add: true);
            count++;
        }

        foreach (PipePlanDEArcDim arc in arcs)
        {
            Vector3d midDir = arc.Mid - arc.Center;
            if (midDir.Length < 1e-9)
            {
                continue;
            }

            // The dimension arc sits `offset` beyond the pipe arc, on its convex (outward) side.
            Point3d arcPoint = arc.Center + (midDir.GetNormal() * (arc.Radius + offset));

            ArcDimension dim = new(arc.Center, arc.Start, arc.End, arcPoint, string.Empty, dimStyleId);
            Configure(dim, db, dimStyleId);
            modelSpace.AppendEntity(dim);
            transaction.AddNewlyCreatedDBObject(dim, add: true);
            count++;
        }

        return count;
    }

    private static void Configure(Dimension dim, Database db, ObjectId dimStyleId)
    {
        dim.SetDatabaseDefaults(db);
        dim.Normal = Vector3d.ZAxis;
        dim.DimensionStyle = dimStyleId;
        dim.Layer = AnnotationLayer;
    }
}
