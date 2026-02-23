using System.Collections.Generic;

namespace DimensioneringV2.BBRData.Models
{
    internal class CsvRowData
    {
        public int RowIndex { get; }
        public string[] RawFields { get; }
        public Dictionary<string, object?> TypedValues { get; }
        public string ComputedKey { get; set; } = string.Empty;

        public CsvRowData(int rowIndex, string[] rawFields)
        {
            RowIndex = rowIndex;
            RawFields = rawFields;
            TypedValues = new Dictionary<string, object?>();
        }

        public string GetDisplayValue(string columnName)
        {
            if (TypedValues.TryGetValue(columnName, out var val))
                return val?.ToString() ?? string.Empty;
            return string.Empty;
        }
    }
}
