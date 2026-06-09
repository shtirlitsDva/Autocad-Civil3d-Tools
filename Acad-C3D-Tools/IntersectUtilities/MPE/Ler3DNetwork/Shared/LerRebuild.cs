using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.Ler3DNetwork
{
    // Shared "lift a 2D drainage polyline to a 3D one" operation. Rewrites the
    // EXISTING Polyline3d's vertices in place instead of recreating it, so the entity
    // keeps its ObjectId, layer, XData, extension dictionary (where Civil 3D property
    // sets live), reactors, and every inbound reference. Must be called inside an
    // active write transaction under a document lock.
    internal static class LerRebuild
    {
        // Returns true when the source was a Polyline3d and got rewritten.
        public static bool ReplacePolyline3d(
            Transaction tx,
            ObjectId sourceId,
            IReadOnlyList<Point3d> newPoints)
        {
            if (tx.GetObject(sourceId, OpenMode.ForWrite, false) is not Polyline3d source)
            {
                return false;
            }

            // SimplePoly drainage lines have no control vertices, so GetVertices
            // returns the real vertices 1:1 with newPoints.
            PolylineVertex3d[] verts = source.GetVertices(tx);
            int shared = Math.Min(verts.Length, newPoints.Count);

            // Move the shared vertices in place — identity, XData and property sets
            // all stay on the same object, so no inbound reference is orphaned.
            for (int i = 0; i < shared; i++)
            {
                verts[i].CheckOrOpenForWrite();
                verts[i].Position = newPoints[i];
            }

            // Append any extra vertices (e.g. the connect path's projected end point).
            for (int i = verts.Length; i < newPoints.Count; i++)
            {
                PolylineVertex3d vertex = new(newPoints[i]);
                source.AppendVertex(vertex);
                tx.AddNewlyCreatedDBObject(vertex, true);
            }

            // Erase any surplus vertices when the new geometry is shorter.
            for (int i = newPoints.Count; i < verts.Length; i++)
            {
                verts[i].CheckOrOpenForWrite();
                verts[i].Erase();
            }

            return true;
        }
    }
}
