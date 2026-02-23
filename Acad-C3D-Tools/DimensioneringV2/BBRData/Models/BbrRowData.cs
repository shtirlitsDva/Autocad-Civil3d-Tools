using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;

namespace DimensioneringV2.BBRData.Models
{
    internal class BbrRowData
    {
        public ObjectId EntityId { get; }
        public Dictionary<string, object?> Values { get; }
        public string ComputedKey { get; set; } = string.Empty;

        public BbrRowData(ObjectId entityId, Dictionary<string, object?> values)
        {
            EntityId = entityId;
            Values = values;
        }

        public string GetDisplayValue(string propertyName)
        {
            if (Values.TryGetValue(propertyName, out var val))
                return val?.ToString() ?? string.Empty;
            return string.Empty;
        }
    }
}
