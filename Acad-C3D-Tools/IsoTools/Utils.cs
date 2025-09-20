using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;

using static IsoTools.Utils;

namespace IsoTools
{
    public static class Utils
    {
        public static void prdDbg(string msg = "") => 
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + msg);
        public static void prdDbg(object obj)
        {
            if (obj is SystemException ex1) prdDbg(obj.ToString());
            else if (obj is System.Exception ex2) prdDbg(obj.ToString());
            else prdDbg(obj.ToString());
        }
    }
    public static class Extensions
    {
        public static HashSet<Oid> HashSetIdsOfType<T>(this Database db) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            HashSet<Oid> objs = new HashSet<Oid>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                RXClass theClass = RXObject.GetClass(typeof(T));
                foreach (Oid oid in modelSpace)
                    if (oid.ObjectClass.IsDerivedFrom(theClass))
                        objs.Add(oid);
                tr.Commit();
            }
            return objs;
        }
        public static List<T> ListOfType<T>(this Database database, Transaction tr, bool discardFrozen = false) where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
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
        }
        public static HashSet<T> HashSetOfType<T>(this Database db, Transaction tr, bool discardFrozen = false)
            where T : Autodesk.AutoCAD.DatabaseServices.Entity
        {
            return new HashSet<T>(db.ListOfType<T>(tr, discardFrozen));
        }
        public static T Go<T>(this Oid oid, Transaction tx, OpenMode openMode = OpenMode.ForRead) where T : DBObject
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
        public static bool IsDerivedFrom<T>(this Oid oid) =>
            oid.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(T)));
        public static string GetAttributeStringValue(this BlockReference br, string attributeName)
        {
            Database db = br.Database;
            Transaction tx = db.TransactionManager.TopTransaction;
            foreach (Oid oid in br.AttributeCollection)
            {
                AttributeReference ar = oid.Go<AttributeReference>(tx);
                if (string.Equals(ar.Tag, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return ar.TextString;
                }
            }

            BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
            foreach (Oid oid in btr)
            {
                if (oid.IsDerivedFrom<AttributeDefinition>())
                {
                    AttributeDefinition attDef = oid.Go<AttributeDefinition>(tx);
                    if (attDef.Constant && string.Equals(attDef.Tag, attributeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return attDef.TextString;
                    }
                }
            }

            return "";
        }
        public static HashSet<Autodesk.AutoCAD.DatabaseServices.BlockReference> GetBlockReferenceByName(
            this Database db, string _BlockName)
        {
            HashSet<Autodesk.AutoCAD.DatabaseServices.BlockReference> set =
                    new HashSet<Autodesk.AutoCAD.DatabaseServices.BlockReference>();

            Transaction tx = db.TransactionManager.TopTransaction;

            BlockTable blkTable = tx.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr;

            if (blkTable.Has(_BlockName))
            {
                prdDbg("Block exists!");
                Oid BlkRecId = blkTable[_BlockName];

                if (BlkRecId != Oid.Null)
                {
                    btr = tx.GetObject(BlkRecId, OpenMode.ForRead) as BlockTableRecord;
                    //Utils.prdDbg("Btr opened!");

                    ObjectIdCollection blockRefIds = btr.IsDynamicBlock ? btr.GetAnonymousBlockIds() : btr.GetBlockReferenceIds(true, true);
                    prdDbg("Number of brefids: " + blockRefIds.Count);

                    foreach (ObjectId blockRefId in blockRefIds)
                    {
                        if (btr.IsDynamicBlock)
                        {
                            ObjectIdCollection oids2 = blockRefId
                                .Go<BlockTableRecord>(tx)
                                .GetBlockReferenceIds(true, true);
                            foreach (Oid oid in oids2)
                                set.Add(oid.Go<BlockReference>(tx));
                        }
                        else { set.Add(blockRefId.Go<BlockReference>(tx)); }
                    }
                    Utils.prdDbg($"Number of refs: {blockRefIds.Count}.");
                }

            }
            return set;
        }
        public static bool IsNoE(this string s) => string.IsNullOrEmpty(s);
    }
}
