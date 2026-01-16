namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for Lag-Ler2.0.v{n}.csv (versioned)
    /// Columns: Layer;Farve;LineType
    /// </summary>
    public sealed class LagLer : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            Layer = 0,
            Farve = 1,
            LineType = 2
        }

        private static readonly string[] _columnNames = { "Layer", "Farve", "LineType" };

        protected override string FilePath => CsvRegistry.GetVersionedFilePath("Lag-Ler2.0");
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Gets a column value for the specified Layer.
        /// </summary>
        public string? Get(string layer, Columns column) => GetValue(layer, (int)column);

        /// <summary>
        /// Gets a column value using a custom key column.
        /// </summary>
        public string? Get(string keyValue, Columns column, Columns keyColumn) =>
            GetValue(keyValue, (int)column, (int)keyColumn);

        // Convenience methods for common lookups
        public string? Layer(string keyValue, Columns keyColumn = Columns.Layer) =>
            GetValue(keyValue, (int)Columns.Layer, (int)keyColumn);

        public string? Farve(string layer) => GetValue(layer, (int)Columns.Farve);
        public string? Farve(string keyValue, Columns keyColumn) =>
            GetValue(keyValue, (int)Columns.Farve, (int)keyColumn);

        public string? LineType(string layer) => GetValue(layer, (int)Columns.LineType);
        public string? LineType(string keyValue, Columns keyColumn) =>
            GetValue(keyValue, (int)Columns.LineType, (int)keyColumn);

        /// <summary>
        /// Checks if the specified Layer exists.
        /// </summary>
        public bool HasLayer(string layer) => ContainsKey(layer);

        /// <summary>
        /// Gets all Layer values.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> AllLayers() =>
            GetAllValuesInColumn((int)Columns.Layer);
    }
}
