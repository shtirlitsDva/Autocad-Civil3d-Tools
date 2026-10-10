namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for Stier.csv (project paths)
    /// Columns: PrjId;Etape;Ler;Surface;Alignments;Fremtid;Længdeprofiler
    /// </summary>
    public sealed class Stier : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            PrjId = 0,
            Etape = 1,
            Ler = 2,
            Surface = 3,
            Alignments = 4,
            Fremtid = 5,
            Laengdeprofiler = 6
        }

        private static readonly string[] _columnNames = 
        { 
            "PrjId", "Etape", "Ler", "Surface", "Alignments", "Fremtid", "Længdeprofiler" 
        };

        protected override string FilePath => CsvRegistry.GetFilePath("Stier.csv");
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Gets a column value for the specified PrjId.
        /// </summary>
        public string? Get(string prjId, Columns column) => GetValue(prjId, (int)column);

        /// <summary>
        /// Gets a column value using a custom key column.
        /// </summary>
        public string? Get(string keyValue, Columns column, Columns keyColumn) =>
            GetValue(keyValue, (int)column, (int)keyColumn);

        // Convenience methods for common lookups
        public string? PrjId(string keyValue, Columns keyColumn = Columns.PrjId) =>
            GetValue(keyValue, (int)Columns.PrjId, (int)keyColumn);

        public string? Etape(string prjId) => GetValue(prjId, (int)Columns.Etape);
        public string? Ler(string prjId) => GetValue(prjId, (int)Columns.Ler);
        public string? Surface(string prjId) => GetValue(prjId, (int)Columns.Surface);
        public string? Alignments(string prjId) => GetValue(prjId, (int)Columns.Alignments);
        public string? Fremtid(string prjId) => GetValue(prjId, (int)Columns.Fremtid);
        public string? Laengdeprofiler(string prjId) => GetValue(prjId, (int)Columns.Laengdeprofiler);

        /// <summary>
        /// Checks if the specified PrjId exists.
        /// </summary>
        public bool HasPrjId(string prjId) => ContainsKey(prjId);

        /// <summary>
        /// Gets all PrjId values.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> AllPrjIds() =>
            GetAllValuesInColumn((int)Columns.PrjId);
    }
}
