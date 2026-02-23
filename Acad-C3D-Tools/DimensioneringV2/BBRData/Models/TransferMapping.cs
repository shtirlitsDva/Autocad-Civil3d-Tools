namespace DimensioneringV2.BBRData.Models
{
    /// <summary>
    /// Pairs a BBR property with a CSV column for data transfer.
    /// The data type is auto-derived from the BBR property — no manual type selection needed.
    /// </summary>
    internal class TransferMapping
    {
        public BbrPropertyDescriptor BbrProperty { get; }
        public string CsvColumnName { get; }

        /// <summary>Type auto-derived from the BBR property.</summary>
        public BbrDataType DataType => BbrProperty.DataType;

        /// <summary>Unique key for dictionary lookups: "BbrPropName/CsvColName".</summary>
        public string Key => $"{BbrProperty.Name}/{CsvColumnName}";

        /// <summary>Column header displayed in the DataGrid.</summary>
        public string DisplayHeader => $"{BbrProperty.Name} / {CsvColumnName}";

        public TransferMapping(BbrPropertyDescriptor bbrProperty, string csvColumnName)
        {
            BbrProperty = bbrProperty;
            CsvColumnName = csvColumnName;
        }

        public override string ToString() => $"{BbrProperty.Name} <- {CsvColumnName} ({DataType})";
    }
}
