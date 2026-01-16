namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// CSV data source for Krydsninger.v{n}.csv (versioned)
    /// Columns: Navn;Layer;Type;Distance;Block;Description;Diameter;Material;System;Status;Kommentar;Temperatur;Tryk;Label
    /// </summary>
    public sealed class Krydsninger : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            Navn = 0,
            Layer = 1,
            Type = 2,
            Distance = 3,
            Block = 4,
            Description = 5,
            Diameter = 6,
            Material = 7,
            System = 8,
            Status = 9,
            Kommentar = 10,
            Temperatur = 11,
            Tryk = 12,
            Label = 13
        }

        private static readonly string[] _columnNames = 
        { 
            "Navn", "Layer", "Type", "Distance", "Block", "Description", 
            "Diameter", "Material", "System", "Status", "Kommentar", 
            "Temperatur", "Tryk", "Label" 
        };

        protected override string FilePath => CsvRegistry.GetVersionedFilePath("Krydsninger");
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Gets a column value for the specified Navn.
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

        public string? Layer(string navn) => GetValue(navn, (int)Columns.Layer);
        public string? Layer(string keyValue, Columns keyColumn) =>
            GetValue(keyValue, (int)Columns.Layer, (int)keyColumn);

        public string? Type(string navn) => GetValue(navn, (int)Columns.Type);
        public string? Distance(string navn) => GetValue(navn, (int)Columns.Distance);
        public string? Block(string navn) => GetValue(navn, (int)Columns.Block);
        public string? Description(string navn) => GetValue(navn, (int)Columns.Description);
        public string? Diameter(string navn) => GetValue(navn, (int)Columns.Diameter);
        public string? Material(string navn) => GetValue(navn, (int)Columns.Material);
        public string? System(string navn) => GetValue(navn, (int)Columns.System);
        public string? Status(string navn) => GetValue(navn, (int)Columns.Status);
        public string? Kommentar(string navn) => GetValue(navn, (int)Columns.Kommentar);
        public string? Temperatur(string navn) => GetValue(navn, (int)Columns.Temperatur);
        public string? Tryk(string navn) => GetValue(navn, (int)Columns.Tryk);
        public string? Label(string navn) => GetValue(navn, (int)Columns.Label);

        /// <summary>
        /// Checks if the specified Navn exists.
        /// </summary>
        public bool HasNavn(string navn) => ContainsKey(navn);

        /// <summary>
        /// Gets all Navn values.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> AllNavne() =>
            GetAllValuesInColumn((int)Columns.Navn);

        /// <summary>
        /// Gets all Layer values.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> AllLayers() =>
            GetAllValuesInColumn((int)Columns.Layer);
    }
}
