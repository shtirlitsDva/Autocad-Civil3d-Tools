using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanPolylineWriter
{
    public static Polyline AppendFromAnalysis(
        Polyline sourcePolyline,
        PipePlanAnalysis analysis,
        PipePlanStoredData metadata,
        BlockTableRecord owner,
        Transaction transaction)
    {
        Polyline polyline = analysis.CreatePolyline();
        polyline.SetDatabaseDefaults(sourcePolyline.Database);
        polyline.SetPropertiesFrom(sourcePolyline);
        polyline.LayerId = sourcePolyline.LayerId;
        polyline.LinetypeId = sourcePolyline.LinetypeId;
        polyline.LineWeight = sourcePolyline.LineWeight;
        polyline.LinetypeScale = sourcePolyline.LinetypeScale;
        polyline.Transparency = sourcePolyline.Transparency;
        polyline.Normal = sourcePolyline.Normal;
        polyline.Elevation = sourcePolyline.Elevation;
        polyline.Thickness = sourcePolyline.Thickness;
        polyline.ConstantWidth = PipePlanWidthCalculator.TryResolveDrawingWidth(
            sourcePolyline.Layer, out double resolvedWidth, out _)
            ? resolvedWidth
            : sourcePolyline.ConstantWidth;
        polyline.Closed = false;

        owner.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, add: true);

        PipePlanMetadata.Write(polyline, metadata, transaction);
        return polyline;
    }
}
