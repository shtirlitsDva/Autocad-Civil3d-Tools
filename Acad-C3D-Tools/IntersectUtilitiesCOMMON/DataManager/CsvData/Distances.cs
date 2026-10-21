namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for Distances.
    /// Configuration-aware with fallback: uses "Distances.{config}.csv" when that dedicated
    /// file exists (e.g. "Distances.DEv1.csv"), otherwise falls back to the shared, unversioned
    /// "Distances.csv" (used by DKv1/DKv2 and when no configuration is selected).
    /// Columns: Type;Distance
    /// </summary>
    public sealed class Distances : CsvDataSource, IConfigurableCsv
    {
        /// <summary>The base file name this source asks for (the single place it is declared).</summary>
        public string BaseName => "Distances";

        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            Type = 0,
            Distance = 1
        }

        private static readonly string[] _columnNames = { "Type", "Distance" };

        protected override string FilePath => CsvRegistry.GetVersionedFilePathOrDefault(BaseName, $"{BaseName}.csv");
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
