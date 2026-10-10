namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for EnhAnvendelse.csv (additional file from register: EnhKoder)
    /// Columns: Nr.;Kode;Beboelse
    /// </summary>
    public sealed class EnhKoder : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            Nr = 0,
            Kode = 1,
            Beboelse = 2
        }

        private static readonly string[] _columnNames = { "Nr.", "Kode", "Beboelse" };

        protected override string FilePath => CsvRegistry.GetAdditionalFilePath("EnhKoder");
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

        public string? Kode(string nr) => GetValue(nr, (int)Columns.Kode);
        public string? Kode(string keyValue, Columns keyColumn) =>
            GetValue(keyValue, (int)Columns.Kode, (int)keyColumn);

        public string? Beboelse(string nr) => GetValue(nr, (int)Columns.Beboelse);

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
