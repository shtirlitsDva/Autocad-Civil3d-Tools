using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.Ler3DNetwork
{
    // Shared "replace a 2D drainage polyline with a lifted 3D one" operation.
    // Builds a new Polyline3d from the given points, keeps the source's layer and
    // properties, carries over every attached record (XData + the full extension
    // dictionary, where Civil 3D property sets live), then erases the original.
    // Must be called inside an active write transaction under a document lock.
    internal static class LerRebuild
    {
        // Returns true when the source was a Polyline3d and got replaced.
        public static bool ReplacePolyline3d(
            Transaction tx,
            ObjectId sourceId,
            IReadOnlyList<Point3d> newPoints)
        {
            if (tx.GetObject(sourceId, OpenMode.ForWrite, false) is not Polyline3d source)
            {
                return false;
            }

            Polyline3d rebuilt = new(
                Poly3dType.SimplePoly,
                new Point3dCollection(newPoints.ToArray()),
                false);
            // SetPropertiesFrom keeps the source layer so the new line stays on the
            // original layer.
            rebuilt.SetPropertiesFrom(source);

            BlockTableRecord owner = (BlockTableRecord)tx.GetObject(source.OwnerId, OpenMode.ForWrite);
            owner.AppendEntity(rebuilt);
            tx.AddNewlyCreatedDBObject(rebuilt, true);

            // Carry over XData + full extension dictionary (property sets, Xrecords,
            // ...) before erasing the old one.
            CopyAttachedData(source, rebuilt, tx);

            source.Erase();
            return true;
        }

        // Copies the source entity's XData and its full extension dictionary onto
        // the rebuilt entity, so the new object inherits every attached record
        // (property sets, pipe tags, Xrecords, nested dictionaries, ...). Both
        // entities must already be database-resident.
        private static void CopyAttachedData(DBObject source, DBObject dest, Transaction tx)
        {
            ResultBuffer? xdata = source.XData;
            if (xdata != null)
            {
                dest.XData = xdata;
                xdata.Dispose();
            }

            if (source.ExtensionDictionary == ObjectId.Null)
            {
                return;
            }

            if (dest.ExtensionDictionary == ObjectId.Null)
            {
                dest.CreateExtensionDictionary();
            }

            DBDictionary sourceDict = (DBDictionary)tx.GetObject(source.ExtensionDictionary, OpenMode.ForRead);
            DBDictionary destDict = (DBDictionary)tx.GetObject(dest.ExtensionDictionary, OpenMode.ForWrite);
            CloneDictionaryEntries(sourceDict, destDict, tx);
        }

        private static void CloneDictionaryEntries(DBDictionary source, DBDictionary dest, Transaction tx)
        {
            foreach (DBDictionaryEntry entry in source)
            {
                if (dest.Contains(entry.Key))
                {
                    continue;
                }

                DBObject obj = tx.GetObject(entry.Value, OpenMode.ForRead);
                if (obj is DBDictionary subSource)
                {
                    DBDictionary subDest = new();
                    dest.SetAt(entry.Key, subDest);
                    tx.AddNewlyCreatedDBObject(subDest, true);
                    CloneDictionaryEntries(subSource, subDest, tx);
                }
                else
                {
                    DBObject clone = (DBObject)obj.Clone();
                    dest.SetAt(entry.Key, clone);
                    tx.AddNewlyCreatedDBObject(clone, true);
                }
            }
        }
    }
}
