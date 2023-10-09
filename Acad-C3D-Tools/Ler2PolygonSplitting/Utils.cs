using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ler2PolygonSplitting
{
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
    }
}
