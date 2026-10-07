using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.LerPathCrawl;
using System.Globalization;

namespace IntersectUtilities.LerProbe;

internal static class LerProbeReader
{
    public static LerCrawlResult<LerProbeSnapshot> Read(
        Transaction hostRead, ObjectId selectedId, IReadOnlyList<ObjectId> containerIds)
    {
        if (hostRead.GetObject(selectedId, OpenMode.ForRead) is not (Polyline or Polyline2d or Polyline3d))
            return LerCrawlResult<LerProbeSnapshot>.Fault("Select a polyline inside the LER xref.");

        var xrefs = new List<string>();
        foreach (ObjectId containerId in containerIds)
        {
            if (hostRead.GetObject(containerId, OpenMode.ForRead) is not BlockReference block)
                continue;
            var record = (BlockTableRecord)hostRead.GetObject(block.BlockTableRecord, OpenMode.ForRead);
            if (record.IsFromExternalReference || record.IsFromOverlayReference)
                xrefs.Add(record.Name);
        }
        if (xrefs.Count == 0)
            return LerCrawlResult<LerProbeSnapshot>.Fault("The selected polyline must be inside an xref.");

        // A nested pick returns the entity's source ObjectId. Read AEC objects
        // with that database's transaction; never attach sets or open for write.
        Database sourceDb = selectedId.Database;
        using var sourceRead = sourceDb.TransactionManager.StartTransaction();
        var pipe = (Entity)sourceRead.GetObject(selectedId, OpenMode.ForRead);
        return ReadProperties(sourceRead, pipe).Match(
            properties => LerCrawlResult<LerProbeSnapshot>.Ok(new(
                string.Join(", ", xrefs.Distinct(StringComparer.OrdinalIgnoreCase)),
                sourceDb.Filename, LerCrawlReader.LocalLayer(pipe.Layer), pipe.Handle.ToString(),
                properties.SetCount, properties.Rows)),
            error => LerCrawlResult<LerProbeSnapshot>.Fault(error));
    }

    internal readonly record struct PropertyRows(int SetCount, IReadOnlyList<LerProbeProperty> Rows);

    internal static LerCrawlResult<PropertyRows> ReadProperties(Transaction sourceRead, DBObject entity)
    {
        try
        {
            var rows = new List<LerProbeProperty>();
            ObjectIdCollection sets = PropertyDataServices.GetPropertySets(entity);
            foreach (ObjectId setId in sets)
            {
                var set = (PropertySet)sourceRead.GetObject(setId, OpenMode.ForRead);
                var definition = (PropertySetDefinition)sourceRead.GetObject(set.PropertySetDefinition, OpenMode.ForRead);
                foreach (PropertyDefinition property in definition.Definitions.Cast<PropertyDefinition>()
                    .OrderBy(item => item.DisplayOrder).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    rows.Add(ReadProperty(set, property, entity));
                }
            }
            return LerCrawlResult<PropertyRows>.Ok(new(sets.Count, rows));
        }
        catch (System.Exception ex)
        {
            // AEC/native API boundary: failed metadata reads are explicit.
            return LerCrawlResult<PropertyRows>.Fault($"Unable to read this polyline's property sets: {ex.Message}");
        }
    }

    private static LerProbeProperty ReadProperty(PropertySet set, PropertyDefinition property, DBObject entity)
    {
        string value;
        bool unavailable = false;
        try
        {
            // Supply the source object so automatic properties have their context.
            value = FormatValue(set.GetAt(property.Id, entity));
        }
        catch (System.Exception ex)
        {
            value = $"<unavailable: {ex.Message}>";
            unavailable = true;
        }
        return new(set.PropertySetDefinitionName, property.Name, value,
            property.DataType.ToString(), property.Description, property.Automatic, unavailable);
    }

    private static string FormatValue(object value) => value switch
    {
        null => "<null returned by AEC>",
        string text => text,
        DateTime date => date.ToString("G", CultureInfo.CurrentCulture),
        Array items => string.Join(", ", items.Cast<object>().Select(FormatValue)),
        IFormattable formatted => formatted.ToString(null, CultureInfo.CurrentCulture) ?? "<unavailable text>",
        _ => value.ToString() ?? "<unavailable text>"
    };
}
