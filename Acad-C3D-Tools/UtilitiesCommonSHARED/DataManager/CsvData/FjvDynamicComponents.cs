namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for FJV Dynamiske Komponenter.csv
    /// Columns: Navn;Type;SysNavn;DN1;DN2;System;Vinkel;Serie;M1;M2;Version;TBLNavn;Function
    /// </summary>
    public sealed class FjvDynamicComponents : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            Navn = 0,
            Type = 1,
            SysNavn = 2,
            DN1 = 3,
            DN2 = 4,
            System = 5,
            Vinkel = 6,
            Serie = 7,
            M1 = 8,
            M2 = 9,
            Version = 10,
            TBLNavn = 11,
            Function = 12
        }

        private static readonly string[] _columnNames = 
        { 
            "Navn", "Type", "SysNavn", "DN1", "DN2", "System", 
            "Vinkel", "Serie", "M1", "M2", "Version", "TBLNavn", "Function" 
        };

        protected override string FilePath => CsvRegistry.GetFilePath("FJV Dynamiske Komponenter.csv");
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Gets a column value for the specified Navn (name).
        /// </summary>
        public string? Get(string navn, Columns column) => GetValue(navn, (int)column);

        /// <summary>
        /// Gets a column value using a custom key column.
        /// </summary>
        public string? Get(string keyValue, Columns column, Columns keyColumn) =>
            GetValue(keyValue, (int)column, (int)keyColumn);

        // Convenience methods for common lookups
        public string? Navn(string keyValue, Columns keyColumn = Columns.Navn) =>
            GetValue(keyValue, (int)Columns.Navn, (int)keyColumn);

        public string? Type(string navn) => GetValue(navn, (int)Columns.Type);
        public string? SysNavn(string navn) => GetValue(navn, (int)Columns.SysNavn);
        public string? DN1(string navn) => GetValue(navn, (int)Columns.DN1);
        public string? DN2(string navn) => GetValue(navn, (int)Columns.DN2);
        public string? System(string navn) => GetValue(navn, (int)Columns.System);
        public string? Vinkel(string navn) => GetValue(navn, (int)Columns.Vinkel);
        public string? Serie(string navn) => GetValue(navn, (int)Columns.Serie);
        public string? M1(string navn) => GetValue(navn, (int)Columns.M1);
        public string? M2(string navn) => GetValue(navn, (int)Columns.M2);
        public string? Version(string navn) => GetValue(navn, (int)Columns.Version);
        public string? TBLNavn(string navn) => GetValue(navn, (int)Columns.TBLNavn);
        public string? Function(string navn) => GetValue(navn, (int)Columns.Function);

        // Version-aware lookup methods for blocks that have VERSION attribute
        public string? Type(string navn, string? version) => GetValueWithVersion(navn, (int)Columns.Type, version);
        public string? DN1(string navn, string? version) => GetValueWithVersion(navn, (int)Columns.DN1, version);
        public string? DN2(string navn, string? version) => GetValueWithVersion(navn, (int)Columns.DN2, version);
        public string? System(string navn, string? version) => GetValueWithVersion(navn, (int)Columns.System, version);
        public string? Serie(string navn, string? version) => GetValueWithVersion(navn, (int)Columns.Serie, version);

        private string? GetValueWithVersion(string navn, int columnIndex, string? version)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(version) || version == "1")
            {
                // No version or version 1 - use standard lookup (first match or non-versioned)
                return GetValue(navn, columnIndex);
            }

            // Version specified - look for matching row with same Navn and Version
            string targetVersion = version.StartsWith("v") ? version : $"v{version}";
            foreach (var row in Rows)
            {
                if (string.Equals(row[(int)Columns.Navn], navn, global::System.StringComparison.OrdinalIgnoreCase))
                {
                    string rowVersion = row[(int)Columns.Version] ?? "";
                    if (string.Equals(rowVersion, targetVersion, global::System.StringComparison.OrdinalIgnoreCase))
                    {
                        return row[columnIndex];
                    }
                }
            }

            // Fallback to standard lookup if no version match found
            return GetValue(navn, columnIndex);
        }

        /// <summary>
        /// Checks if the specified Navn exists.
        /// </summary>
        public bool HasNavn(string navn) => ContainsKey(navn);

        /// <summary>
        /// Gets all Navn values.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> AllNavne() =>
            GetAllValuesInColumn((int)Columns.Navn);
    }
}
