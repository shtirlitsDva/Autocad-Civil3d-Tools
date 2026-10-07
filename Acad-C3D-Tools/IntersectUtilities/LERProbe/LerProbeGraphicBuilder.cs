using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.LerPathCrawl;
using CadColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities.LerProbe;

internal readonly record struct LerProbeGraphic(IReadOnlyList<Entity> Entities, bool Blue) : IDisposable
{
    public void Dispose()
    {
        foreach (Entity entity in Entities)
            entity.Dispose();
    }
}

internal static class LerProbeGraphicBuilder
{
    public static LerCrawlResult<LerProbeGraphic> Read(
        Transaction read, Database hostDb, LerProbeHighlightPath path, Matrix3d transform)
    {
        var entities = new List<Entity>();
        try
        {
            ObjectId[] ids = path.Path.GetObjectIds();
            var source = (Entity)read.GetObject(ids[^1], OpenMode.ForRead);
            bool blue = IsRed(DisplayedColor(read, hostDb, ids));
            try
            {
                entities.Add(source.GetTransformedCopy(transform));
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == ErrorStatus.ExplodeBeforeTransform)
            {
                // Nonuniformly scaled polyline arcs become ellipses. Native
                // exploded segment copies preserve that geometry without sampling.
                using var pieces = new DBObjectCollection();
                try
                {
                    source.Explode(pieces);
                    foreach (DBObject piece in pieces)
                        if (piece is Entity segment)
                            entities.Add(segment.GetTransformedCopy(transform));
                }
                finally
                {
                    foreach (DBObject piece in pieces)
                        piece.Dispose();
                }
            }
            foreach (Entity entity in entities)
            {
                entity.SetDatabaseDefaults(hostDb);
                entity.LayerId = hostDb.LayerZero;
                entity.Color = blue ? CadColor.FromRgb(0, 0, 255) : CadColor.FromRgb(255, 0, 0);
                entity.Transparency = new Transparency(255);
                entity.LineWeight = LineWeight.LineWeight100;
                entity.Linetype = "Continuous";
                entity.Visible = true;
            }
            return LerCrawlResult<LerProbeGraphic>.Ok(new(entities, blue));
        }
        catch (System.Exception ex)
        {
            foreach (Entity entity in entities)
                entity.Dispose();
            return LerCrawlResult<LerProbeGraphic>.Fault($"Unable to draw the selected LER polyline: {ex.Message}");
        }
    }

    internal static bool IsRed(System.Drawing.Color color)
    {
        float hue = color.GetHue();
        return color.GetSaturation() > 0.15F && (hue < 20F || hue > 340F);
    }

    internal static System.Drawing.Color DisplayedColor(Transaction read, Database hostDb, IReadOnlyList<ObjectId> path)
    {
        var layers = (LayerTable)read.GetObject(hostDb.LayerTableId, OpenMode.ForRead);
        var prefixes = new List<string>();
        System.Drawing.Color parentColor = System.Drawing.Color.White;
        System.Drawing.Color parentLayer = System.Drawing.Color.White;
        bool nested = false;
        foreach (ObjectId id in path)
        {
            var entity = (Entity)read.GetObject(id, OpenMode.ForRead);
            string leafLayer = LerCrawlReader.LocalLayer(entity.Layer);
            System.Drawing.Color layerColor;
            if (leafLayer == "0" && nested)
                layerColor = parentLayer;
            else
            {
                // Host xref-layer overrides determine the displayed ByLayer colour.
                string qualified = string.Join("|", prefixes.Append(leafLayer));
                ObjectId layerId = layers.Has(qualified) ? layers[qualified] : entity.LayerId;
                var layer = (LayerTableRecord)read.GetObject(layerId, OpenMode.ForRead);
                using var layerNativeColor = layer.Color;
                layerColor = layerNativeColor.ColorValue;
            }
            using var color = entity.Color;
            parentColor = color.IsByBlock ? parentColor : color.IsByLayer ? layerColor : color.ColorValue;
            parentLayer = layerColor;
            nested = true;
            if (entity is BlockReference block)
            {
                var record = (BlockTableRecord)read.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                if (record.IsFromExternalReference || record.IsFromOverlayReference)
                    prefixes.Add(LerCrawlReader.LocalLayer(record.Name));
            }
        }
        return parentColor;
    }
}
