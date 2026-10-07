using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.LerPathCrawl;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.LerProbe;

internal readonly record struct LerProbeHighlightPath(ObjectId RootId, FullSubentityPath Path);

internal sealed class LerProbeHighlight : IDisposable
{
    private readonly Document document;
    private readonly LerProbeGraphic graphic;
    private readonly IntegerCollection viewports = new();
    private bool active;
    private bool disposed;

    public LerProbeHighlight(Document document, LerProbeGraphic graphic)
    {
        this.document = document;
        this.graphic = graphic;
    }

    public static LerCrawlResult<LerProbeHighlightPath> Resolve(
        Transaction read, ObjectId selectedId, IReadOnlyList<ObjectId> containerIds)
    {
        if (containerIds.Count == 0)
            return LerCrawlResult<LerProbeHighlightPath>.Fault("Unable to highlight a polyline without an xref container.");

        var containers = new List<BlockReference>();
        foreach (ObjectId id in containerIds)
        {
            if (read.GetObject(id, OpenMode.ForRead) is not BlockReference block)
                return LerCrawlResult<LerProbeHighlightPath>.Fault("Unable to resolve the selected xref's highlight path.");
            containers.Add(block);
        }
        var ids = new List<ObjectId> { selectedId };
        ObjectId owner = read.GetObject(selectedId, OpenMode.ForRead).OwnerId;
        while (containers.Count > 0)
        {
            // Resolve containment instead of relying on GetContainers array order.
            int index = containers.FindIndex(block => LerCrawlReader.Contains(read, block, owner));
            if (index < 0)
                return LerCrawlResult<LerProbeHighlightPath>.Fault("Unable to resolve the selected xref's highlight path.");
            BlockReference container = containers[index];
            ids.Add(container.ObjectId);
            owner = container.OwnerId;
            containers.RemoveAt(index);
        }
        ids.Reverse();
        var fullPath = new FullSubentityPath(ids.ToArray(), new SubentityId(SubentityType.Null, IntPtr.Zero));
        return LerCrawlResult<LerProbeHighlightPath>.Ok(new(ids[0], fullPath));
    }

    public LerCrawlResult<bool> Show()
    {
        if (disposed)
            return LerCrawlResult<bool>.Fault("The LERPROBE highlight has been closed.");
        if (active)
            return LerCrawlResult<bool>.Ok(true);
        if (document != Application.DocumentManager.MdiActiveDocument || graphic.Entities.Count == 0)
            return LerCrawlResult<bool>.Ok(false);
        var added = new List<Entity>();
        try
        {
            foreach (Entity entity in graphic.Entities)
            {
                if (!TransientManager.CurrentTransientManager.AddTransient(
                    entity, TransientDrawingMode.DirectTopmost, 131, viewports))
                {
                    Erase(added);
                    return LerCrawlResult<bool>.Fault("AutoCAD could not display the LERPROBE highlight.");
                }
                added.Add(entity);
            }
            active = true;
            return LerCrawlResult<bool>.Ok(true);
        }
        catch (System.Exception ex)
        {
            Erase(added);
            return LerCrawlResult<bool>.Fault($"Unable to highlight the selected LER polyline: {ex.Message}");
        }
    }

    public bool IsVisible => active;

    public void Hide()
    {
        if (!active)
            return;
        Erase(graphic.Entities);
        active = false;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        Hide();
        graphic.Dispose();
        disposed = true;
    }

    private void Erase(IEnumerable<Entity> entities)
    {
        foreach (Entity entity in entities)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(entity, viewports);
            }
            catch (System.Exception ex)
            {
                UtilsCommon.Utils.prdDbg(ex);
            }
        }
    }
}
