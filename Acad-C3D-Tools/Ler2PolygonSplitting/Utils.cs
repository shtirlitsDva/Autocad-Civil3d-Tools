using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

namespace Ler2PolygonSplitting
{
    public static class Utils
    {
        public static Dictionary<string, Color> AutocadStdColors = new Dictionary<string, Color>()
        {
            {"byblock", Color.FromColorIndex(ColorMethod.ByAci, 0) },
            {"red", Color.FromColorIndex(ColorMethod.ByAci, 1) },
            {"yellow", Color.FromColorIndex(ColorMethod.ByAci, 2) },
            {"green", Color.FromColorIndex(ColorMethod.ByAci, 3) },
            {"cyan", Color.FromColorIndex(ColorMethod.ByAci, 4) },
            {"blue", Color.FromColorIndex(ColorMethod.ByAci, 5) },
            {"magenta", Color.FromColorIndex(ColorMethod.ByAci, 6) },
            {"white", Color.FromColorIndex(ColorMethod.ByAci, 7) },
            {"grey", Color.FromColorIndex(ColorMethod.ByAci, 8) },
            {"bylayer", Color.FromColorIndex(ColorMethod.ByAci, 256) },
        };
        public static void prdDbg(string msg)
        {
            DocumentCollection docCol = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;
            editor.WriteMessage("\n" + msg);
        }
        public static void prdDbg(object obj)
        {
            if (obj is SystemException ex1) prdDbg(obj.ToString());
            else if (obj is System.Exception ex2) prdDbg(obj.ToString());
            else prdDbg(obj.ToString());
        }
        public static void prdDbgIL(string msg) =>
            Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(msg);
    }
    public static class Extensions
    {
        public static bool CheckOrCreateLayer(this Database db, string layerName, short colorIdx = -1, bool isPlottable = true)
        {
            Transaction txLag = db.TransactionManager.TopTransaction;
            LayerTable lt = txLag.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (!lt.Has(layerName))
            {
                LayerTableRecord ltr = new LayerTableRecord();
                ltr.Name = layerName;
                ltr.IsPlottable = isPlottable;
                if (colorIdx != -1)
                {
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx);
                }

                //Make layertable writable
                lt.UpgradeOpen();

                //Add the new layer to layer table
                Oid ltId = lt.Add(ltr);
                txLag.AddNewlyCreatedDBObject(ltr, true);
                return true;
            }
            else
            {
                if (colorIdx == -1) return true;
                LayerTableRecord ltr = txLag.GetObject(lt[layerName], OpenMode.ForWrite) as LayerTableRecord;
                if (ltr.Color.ColorIndex != colorIdx)
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx);
                return true;
            }
        }
        public static List<T> ListOfType<T>(this Database database, Transaction tr, bool discardFrozen = false) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            //using (var tr = database.TransactionManager.StartTransaction())
            //{

            //Init the list of the objects
            List<T> objs = new List<T>();

            // Get the block table for the current database
            var blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);

            // Get the model space block table record
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            RXClass theClass = RXObject.GetClass(typeof(T));

            // Loop through the entities in model space
            foreach (Oid oid in modelSpace)
            {
                // Look for entities of the correct type
                if (oid.ObjectClass.IsDerivedFrom(theClass))
                {
                    var entity = (T)tr.GetObject(oid, OpenMode.ForRead);
                    if (discardFrozen)
                    {
                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
                        if (layer.IsFrozen) continue;
                    }

                    objs.Add(entity);
                }
            }
            return objs;
            //tr.Commit();
            //}
        }
        public static HashSet<T> HashSetOfType<T>(this Database db, Transaction tr, bool discardFrozen = false)
            where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            return new HashSet<T>(db.ListOfType<T>(tr, discardFrozen));
        }
        public static Oid AddEntityToDbModelSpace<T>(this T entity, Database db) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            if (db.TransactionManager.TopTransaction == null)
            {
                using (Transaction tx = db.TransactionManager.StartTransaction())
                {
                    try
                    {

                        BlockTableRecord modelSpace = db.GetModelspaceForWrite();
                        Oid id = modelSpace.AppendEntity(entity);
                        tx.AddNewlyCreatedDBObject(entity, true);
                        tx.Commit();
                        return id;
                    }
                    catch (System.Exception)
                    {
                        Utils.prdDbg("Adding element to database failed!");
                        tx.Abort();
                        return Oid.Null;
                    }
                }
            }
            else
            {
                Transaction tx = db.TransactionManager.TopTransaction;

                BlockTableRecord modelSpace = db.GetModelspaceForWrite();
                Oid id = modelSpace.AppendEntity(entity);
                tx.AddNewlyCreatedDBObject(entity, true);
                return id;
            }
        }
        public static BlockTableRecord GetModelspaceForWrite(this Database db) =>
            db.BlockTableId.Go<BlockTable>(db.TransactionManager.TopTransaction)[BlockTableRecord.ModelSpace]
            .Go<BlockTableRecord>(db.TransactionManager.TopTransaction, OpenMode.ForWrite);
        public static T Go<T>(this Oid oid, Transaction tx,
            Autodesk.AutoCAD.DatabaseServices.OpenMode openMode =
            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) where T : Autodesk.AutoCAD.DatabaseServices.DBObject
        {
            var obj = tx.GetObject(oid, openMode, false);
            if (obj is T) return (T)obj;
            else return null;
        }
        public static T Go<T>(this Handle handle, Database database) where T : Autodesk.AutoCAD.DatabaseServices.DBObject
        {
            Oid id = database.GetObjectId(false, handle, 0);
            if (database.TransactionManager.TopTransaction == null)
                throw new System.Exception("Handle.Go<DBObject> -> no top transaction found! Call inside transaction.");
            return id.Go<T>(database.TransactionManager.TopTransaction);
        }
        public static T Go<T>(this Database db, string handle) where T : Autodesk.AutoCAD.DatabaseServices.DBObject
        {
            Handle h = new Handle(Convert.ToInt64(handle, 16));
            return h.Go<T>(db);
        }
    }
}
