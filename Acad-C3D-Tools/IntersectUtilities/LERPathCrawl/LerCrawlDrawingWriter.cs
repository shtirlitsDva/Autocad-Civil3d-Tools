using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.LerPathCrawl;

internal static class LerCrawlDrawingWriter
{
    // The command owns the transaction and commits only after the centreline is
    // appended. Keeping this step separate also lets validation roll it back.
    public static ObjectId Append(Database db, Transaction tr, Polyline centerline)
    {
        var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        ObjectId layerId;
        if (layers.Has(LerCrawlSettings.Layer))
        {
            layerId = layers[LerCrawlSettings.Layer];
            var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
            if (layer.IsOff)
                layer.IsOff = false;
            // AutoCAD rejects this setter for the active drawing's current
            // layer even when assigning false to an already-thawed layer.
            if (layer.IsFrozen)
                layer.IsFrozen = false;
        }
        else
        {
            layers.UpgradeOpen();
            using var layer = new LayerTableRecord
            {
                Name = LerCrawlSettings.Layer,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 2)
            };
            layerId = layers.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }
        var blocks = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var model = (BlockTableRecord)tr.GetObject(blocks[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        centerline.SetDatabaseDefaults(db);
        centerline.LayerId = layerId;
        centerline.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
        ObjectId id = model.AppendEntity(centerline);
        tr.AddNewlyCreatedDBObject(centerline, true);
        return id;
    }
}
