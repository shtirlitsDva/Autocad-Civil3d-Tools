using System.Collections.Generic;
using System.Globalization;

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

        /// <summary>
        /// Returns display value always using '.' for decimals (InvariantCulture).
        /// AutoCAD internally uses dot decimals.
        /// </summary>
        public string GetDisplayValue(string propertyName)
        {
            if (Values.TryGetValue(propertyName, out var val))
            {
                return val switch
                {
                    double d => d.ToString(CultureInfo.InvariantCulture),
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    _ => val?.ToString() ?? string.Empty
                };
            }
            return string.Empty;
        }
    }
}
