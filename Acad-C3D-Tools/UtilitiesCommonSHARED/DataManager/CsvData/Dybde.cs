namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for Dybde.
    /// Configuration-aware with fallback: uses "Dybde.{config}.csv" when that dedicated file
    /// exists (e.g. "Dybde.DEv1.csv"), otherwise falls back to the shared, unversioned
    /// "Dybde.csv" (used by DKv1/DKv2 and when no configuration is selected).
    /// Columns: Type;Dybde
    /// </summary>
    public sealed class Dybde : CsvDataSource, IConfigurableCsv
    {
        /// <summary>The base file name this source asks for (the single place it is declared).</summary>
        public string BaseName => "Dybde";

        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            Type = 0,
            DybdeValue = 1  // Named DybdeValue to avoid conflict with class name
        }

        private static readonly string[] _columnNames = { "Type", "Dybde" };

        protected override string FilePath => CsvRegistry.GetVersionedFilePathOrDefault(BaseName, $"{BaseName}.csv");
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Gets the Dybde value for the specified Type.
        /// </summary>
        /// <param name="type">The type to look up (e.g., "VAND").</param>
        /// <returns>The dybde value, or null if not found.</returns>
        public string? DybdeFor(string type) => GetValue(type, (int)Columns.DybdeValue);

        /// <summary>
        /// Gets the Dybde value using a custom key column.
        /// </summary>
        public string? DybdeFor(string keyValue, Columns keyColumn) =>
            GetValue(keyValue, (int)Columns.DybdeValue, (int)keyColumn);

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

        /// <summary>
        /// Gets the Dybde value as a double for the specified Type.
        /// Returns 0 if not found or cannot be parsed.
        /// </summary>
        public double GetDybdeDouble(string type)
        {
            string? value = DybdeFor(type);
            if (string.IsNullOrEmpty(value))
                return 0;
            if (double.TryParse(value, System.Globalization.NumberStyles.AllowDecimalPoint, 
                System.Globalization.CultureInfo.InvariantCulture, out double result))
                return result;
            return 0;
        }
    }
}
