namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for Register-2025.csv (DLL loading registry)
    /// Columns: DisplayName;Path
    /// </summary>
    public sealed class NsLoadRegister : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            DisplayName = 0,
            Path = 1
        }

        private static readonly string[] _columnNames = { "DisplayName", "Path" };

        protected override string FilePath => CsvRegistry.GetAdditionalFilePath("NsLoadRegister");
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Gets a column value for the specified DisplayName.
        /// </summary>
        public string? Get(string displayName, Columns column) => GetValue(displayName, (int)column);

        // Convenience methods
        public string? DisplayName(string keyValue, Columns keyColumn = Columns.DisplayName) =>
            GetValue(keyValue, (int)Columns.DisplayName, (int)keyColumn);

        public string? Path(string displayName) => GetValue(displayName, (int)Columns.Path);

        /// <summary>
        /// Gets all DisplayName values.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> AllDisplayNames() =>
            GetAllValuesInColumn((int)Columns.DisplayName);

        /// <summary>
        /// Checks if the specified DisplayName exists.
        /// </summary>
        public bool HasDisplayName(string displayName) => ContainsKey(displayName);
    }
}
