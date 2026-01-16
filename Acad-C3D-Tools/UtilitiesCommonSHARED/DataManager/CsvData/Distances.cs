namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for Distances.csv
    /// Columns: Type;Distance
    /// </summary>
    public sealed class Distances : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            Type = 0,
            Distance = 1
        }

        private static readonly string[] _columnNames = { "Type", "Distance" };

        protected override string FilePath => CsvRegistry.GetFilePath("Distances.csv");
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Gets the Distance value for the specified Type.
        /// </summary>
        /// <param name="type">The type to look up (e.g., "FJV", "Vand").</param>
        /// <returns>The distance value, or null if not found.</returns>
        public string? Distance(string type) => GetValue(type, (int)Columns.Distance);

        /// <summary>
        /// Gets the Distance value using a custom key column.
        /// </summary>
        public string? Distance(string keyValue, Columns keyColumn) =>
            GetValue(keyValue, (int)Columns.Distance, (int)keyColumn);

        /// <summary>
        /// Gets the Type value for a lookup.
        /// </summary>
        public string? Type(string keyValue, Columns keyColumn) =>
            GetValue(keyValue, (int)Columns.Type, (int)keyColumn);

        /// <summary>
        /// Checks if the specified type exists.
        /// </summary>
        public bool HasType(string type) => ContainsKey(type);

        /// <summary>
        /// Gets all Type values.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> AllTypes() =>
            GetAllValuesInColumn((int)Columns.Type);
    }
}
