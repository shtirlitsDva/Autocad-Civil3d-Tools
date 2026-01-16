namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for BygAnvendelse.csv (additional file from register: AnvKoder)
    /// Columns: Nr.;Betegnelse;Beholdes;Erhverv;Translation
    /// </summary>
    public sealed class AnvKoder : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            Nr = 0,
            Betegnelse = 1,
            Beholdes = 2,
            Erhverv = 3,
            Translation = 4
        }

        private static readonly string[] _columnNames = 
        { 
            "Nr.", "Betegnelse", "Beholdes", "Erhverv", "Translation" 
        };

        protected override string FilePath => CsvRegistry.GetAdditionalFilePath("AnvKoder");
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Gets a column value for the specified Nr.
        /// </summary>
        public string? Get(string nr, Columns column) => GetValue(nr, (int)column);

        /// <summary>
        /// Gets a column value using a custom key column.
        /// </summary>
        public string? Get(string keyValue, Columns column, Columns keyColumn) =>
            GetValue(keyValue, (int)column, (int)keyColumn);

        // Convenience methods for common lookups
        public string? Nr(string keyValue, Columns keyColumn = Columns.Nr) =>
            GetValue(keyValue, (int)Columns.Nr, (int)keyColumn);

        public string? Betegnelse(string nr) => GetValue(nr, (int)Columns.Betegnelse);
        public string? Beholdes(string nr) => GetValue(nr, (int)Columns.Beholdes);
        public string? Erhverv(string nr) => GetValue(nr, (int)Columns.Erhverv);
        public string? Translation(string nr) => GetValue(nr, (int)Columns.Translation);

        /// <summary>
        /// Checks if the specified Nr exists.
        /// </summary>
        public bool HasNr(string nr) => ContainsKey(nr);

        /// <summary>
        /// Gets all Nr values.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> AllNrs() =>
            GetAllValuesInColumn((int)Columns.Nr);
    }
}
