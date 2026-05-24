using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.MPE.PipePlan;

// Reshapes an existing polyline to match a PipePlanAnalysis in place, preserving
// Handle, ObjectId, ExtensionDictionary, and any third-party Xdata/Xrecord content.
// Vertex count is allowed to differ (fillet recompute is the dominant case): we
// overwrite the overlap range with SetPointAt/SetBulgeAt and adjust the tail with
// AddVertexAt/RemoveVertexAt. The caller is responsible for opening `target`
// ForWrite inside its own transaction.
internal static class PipePlanPolylineMutator
{
    public static void ApplyAnalysis(
        Polyline target,
        PipePlanAnalysis analysis,
        PipePlanStoredData metadata,
        string layerName,
        Transaction transaction)
    {
        int oldCount = target.NumberOfVertices;
        int newCount = analysis.Vertices.Count;
        int overlap = Math.Min(oldCount, newCount);

        for (int i = 0; i < overlap; i++)
        {
            PolylineVertexData vertex = analysis.Vertices[i];
            target.SetPointAt(i, vertex.Point);
            target.SetBulgeAt(i, vertex.Bulge);
            target.SetStartWidthAt(i, 0.0);
            target.SetEndWidthAt(i, 0.0);
        }

        if (newCount > oldCount)
        {
            for (int j = oldCount; j < newCount; j++)
            {
                PolylineVertexData vertex = analysis.Vertices[j];
                target.AddVertexAt(j, vertex.Point, vertex.Bulge, 0.0, 0.0);
            }
        }
        else if (newCount < oldCount)
        {
            // Remove from the tail downward — RemoveVertexAt shifts trailing
            // indices, so descending traversal keeps the remaining indices stable.
            for (int j = oldCount - 1; j >= newCount; j--)
            {
                target.RemoveVertexAt(j);
            }
        }

        target.Layer = layerName;
        target.ConstantWidth = PipePlanWidthCalculator.ResolveDrawingWidth(layerName);
        target.Closed = false;

        PipePlanMetadata.Write(target, metadata, transaction);
    }
}
