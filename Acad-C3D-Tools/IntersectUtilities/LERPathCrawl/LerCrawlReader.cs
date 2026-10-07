using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LerPathCrawl;

internal static class LerCrawlReader
{
    public static LerCrawlResult<LerCrawlSource> Read(Transaction tr, PromptNestedEntityResult pick, Matrix3d ucs)
        => ReadSelection(tr, pick.ObjectId, pick.GetContainers(), pick.PickedPoint, ucs);

    internal static LerCrawlResult<LerCrawlSource> ReadSelection(Transaction tr, ObjectId selectedId,
        IReadOnlyList<ObjectId> containerIds, Point3d pickedPoint, Matrix3d ucs)
    {
        if (tr.GetObject(selectedId, OpenMode.ForRead) is not Polyline selected)
            return LerCrawlResult<LerCrawlSource>.Fault("Select a lightweight LER polyline inside an xref.");

        var containers = containerIds
            .Select(id => (BlockReference)tr.GetObject(id, OpenMode.ForRead)).ToList();
        // Reconstruct containment by OwnerId rather than depending on array order.
        var hierarchy = new List<BlockReference>();
        ObjectId owner = selected.OwnerId;
        while (containers.Count > 0)
        {
            int index = containers.FindIndex(block => Contains(tr, block, owner));
            if (index < 0)
                return LerCrawlResult<LerCrawlSource>.Fault("Unable to resolve the selected xref instance.");
            var block = containers[index];
            hierarchy.Add(block);
            owner = block.OwnerId;
            containers.RemoveAt(index);
        }

        int root = -1;
        for (int index = 0; index < hierarchy.Count; index++)
        {
            var record = (BlockTableRecord)tr.GetObject(hierarchy[index].BlockTableRecord, OpenMode.ForRead);
            if (record.IsFromExternalReference || record.IsFromOverlayReference)
                root = index;
        }
        if (root < 0)
            return LerCrawlResult<LerCrawlSource>.Fault("The first polyline must be inside an attached LER xref.");

        Matrix3d toWorld = Matrix3d.Identity;
        for (int index = root; index < hierarchy.Count; index++)
            toWorld = hierarchy[index].BlockTransform * toWorld;

        string layer = LocalLayer(selected.Layer);
        var rootRecord = (BlockTableRecord)tr.GetObject(hierarchy[root].BlockTableRecord, OpenMode.ForRead);
        return ReadRecord(tr, rootRecord.ObjectId, toWorld, layer).Match(
            segments => segments.Items.Count == 0
                ? LerCrawlResult<LerCrawlSource>.Fault($"No usable LER polylines on layer {layer} in this xref.")
                : LerCrawlResult<LerCrawlSource>.Ok(new(segments.Items, selectedId, layer,
                    rootRecord.Name, pickedPoint.TransformBy(ucs))),
            error => LerCrawlResult<LerCrawlSource>.Fault(error));
    }

    internal static bool Contains(Transaction tr, BlockReference block, ObjectId owner)
    {
        if (block.BlockTableRecord == owner)
            return true;
        var record = (BlockTableRecord)tr.GetObject(block.BlockTableRecord, OpenMode.ForRead);
        if (!record.IsFromExternalReference && !record.IsFromOverlayReference)
            return false;
        // Xref entities keep their source model-space OwnerId, whereas the container
        // points to the host's xref definition. Those are different databases/IDs.
        Database referenced = record.GetXrefDatabase(false);
        var blocks = (BlockTable)tr.GetObject(referenced.BlockTableId, OpenMode.ForRead);
        return blocks[BlockTableRecord.ModelSpace] == owner;
    }

    internal readonly record struct SegmentList(IReadOnlyList<LerCrawlSegment> Items);

    internal static LerCrawlResult<SegmentList> ReadRecord(Transaction tr, ObjectId recordId,
        Matrix3d transform, string layer)
    {
        var segments = new List<LerCrawlSegment>();
        var ancestry = new HashSet<ObjectId>();
        string error = string.Empty;

        bool Walk(ObjectId id, Matrix3d toWorld)
        {
            if (!ancestry.Add(id))
            {
                error = "A circular block reference prevents reading this LER xref.";
                return false;
            }
            var record = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
            foreach (ObjectId entityId in record)
            {
                var entity = tr.GetObject(entityId, OpenMode.ForRead);
                if (entity is Polyline polyline &&
                    string.Equals(LocalLayer(polyline.Layer), layer, StringComparison.OrdinalIgnoreCase))
                {
                    bool read = ReadPolyline(polyline, toWorld).Match(
                        result => { segments.AddRange(result.Items); return true; },
                        message => { error = message; return false; });
                    if (!read)
                        return false;
                }
                else if (entity is BlockReference block)
                {
                    if (!Walk(block.BlockTableRecord, toWorld * block.BlockTransform))
                        return false;
                }
            }
            ancestry.Remove(id);
            return true;
        }

        return Walk(recordId, transform)
            ? LerCrawlResult<SegmentList>.Ok(new(segments))
            : LerCrawlResult<SegmentList>.Fault(error);
    }

    internal static LerCrawlResult<SegmentList> ReadPolyline(Polyline source, Matrix3d transform)
    {
        var cs = transform.CoordinateSystem3d;
        double sx = cs.Xaxis.Length;
        double sy = cs.Yaxis.Length;
        if (sx <= 0 || sy <= 0 || Math.Abs(sx - sy) > Math.Max(sx, sy) * 1e-9 ||
            Math.Abs(cs.Xaxis.DotProduct(cs.Yaxis)) > sx * sy * 1e-9 ||
            Math.Abs(cs.Xaxis.Z) > sx * 1e-9 || Math.Abs(cs.Yaxis.Z) > sy * 1e-9 ||
            Math.Abs(Math.Abs(source.Normal.Z) - 1) > 1e-9)
            return LerCrawlResult<SegmentList>.Fault(
                "LERCRAWL requires horizontal 2D polylines and a uniform plan scale; tilted/nonuniform geometry cannot retain circular centreline arcs.");

        using var polyline = (Polyline)source.Clone();
        polyline.TransformBy(transform);
        double sign = Math.Sign(polyline.Normal.Z);
        var segments = new List<LerCrawlSegment>();
        int count = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
        for (int index = 0; index < count; index++)
        {
            Point3d start = polyline.GetPoint3dAt(index);
            Point3d end = polyline.GetPoint3dAt((index + 1) % polyline.NumberOfVertices);
            if (start.DistanceTo(end) <= LerCrawlSettings.GeometryTolerance)
                continue;
            segments.Add(new(new Point2d(start.X, start.Y), new Point2d(end.X, end.Y),
                sign * polyline.GetBulgeAt(index), polyline.GetStartWidthAt(index),
                polyline.GetEndWidthAt(index), source.ObjectId));
        }
        return LerCrawlResult<SegmentList>.Ok(new(segments));
    }

    public static string LocalLayer(string layer) => layer[(layer.LastIndexOf('|') + 1)..];
}
