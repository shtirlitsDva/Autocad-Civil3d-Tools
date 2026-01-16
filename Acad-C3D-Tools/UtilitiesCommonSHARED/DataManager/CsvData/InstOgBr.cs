namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for Installation og brændsel.csv (additional file from register)
    /// Columns: Installation og brændsel;Type
    /// </summary>
    public sealed class InstOgBr : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            InstallationOgBraendsel = 0,
            Type = 1
        }

        private static readonly string[] _columnNames = { "Installation og brændsel", "Type" };

        protected override string FilePath => CsvRegistry.GetAdditionalFilePath("instogbr");
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Gets the Type value for the specified Installation og brændsel.
        /// </summary>
        public string? Type(string installationOgBraendsel) => 
            GetValue(installationOgBraendsel, (int)Columns.Type);

        /// <summary>
        /// Gets the Type value using a custom key column.
        /// </summary>
        public string? Type(string keyValue, Columns keyColumn) =>
            GetValue(keyValue, (int)Columns.Type, (int)keyColumn);

        /// <summary>
        /// Gets the InstallationOgBraendsel value for a lookup.
        /// </summary>
        public string? InstallationOgBraendsel(string keyValue, Columns keyColumn) =>
            GetValue(keyValue, (int)Columns.InstallationOgBraendsel, (int)keyColumn);

        /// <summary>
        /// Checks if the specified installation og brændsel exists.
        /// </summary>
        public bool HasInstallation(string installation) => ContainsKey(installation);

        /// <summary>
        /// Gets all Installation og brændsel values.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> AllInstallations() =>
            GetAllValuesInColumn((int)Columns.InstallationOgBraendsel);
    }
}
