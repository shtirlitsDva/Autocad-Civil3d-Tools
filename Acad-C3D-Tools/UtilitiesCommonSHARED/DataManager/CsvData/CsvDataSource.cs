using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualBasic.FileIO;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// Abstract base class for CSV data sources with lazy loading and auto-reload on file change.
    /// </summary>
    public abstract class CsvDataSource
    {
        private readonly object _lock = new();
        private DateTime _lastLoadTime = DateTime.MinValue;
        private DateTime _lastFileModified = DateTime.MinValue;
        private bool _isLoaded = false;

        /// <summary>
        /// Gets the full path to the CSV file.
        /// </summary>
        protected abstract string FilePath { get; }

        /// <summary>
        /// Gets the column names in order as they appear in the CSV.
        /// </summary>
        protected abstract string[] ColumnNames { get; }

        /// <summary>
        /// The default key column index (usually 0 - first column).
        /// </summary>
        protected virtual int DefaultKeyColumnIndex => 0;

        /// <summary>
        /// Storage for CSV data: KeyColumnIndex -> KeyValue -> ColumnIndex -> Value
        /// </summary>
        private readonly Dictionary<int, Dictionary<string, Dictionary<int, string>>> _data = new();

        /// <summary>
        /// Raw rows for iteration.
        /// </summary>
        private readonly List<string[]> _rows = new();

        /// <summary>
        /// Gets all rows in the CSV.
        /// </summary>
        public IReadOnlyList<string[]> Rows
        {
            get
            {
                EnsureLoaded();
                return _rows;
            }
        }

        /// <summary>
        /// Gets the number of rows in the CSV.
        /// </summary>
        public int RowCount
        {
            get
            {
                EnsureLoaded();
                return _rows.Count;
            }
        }

        /// <summary>
        /// Ensures data is loaded and reloads if the file has been modified.
        /// </summary>
        protected void EnsureLoaded()
        {
            lock (_lock)
            {
                string filePath = FilePath; // Evaluate once
                
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"CSV file not found: {filePath}");
                }

                DateTime currentModified = File.GetLastWriteTime(filePath);

                if (!_isLoaded || currentModified > _lastFileModified)
                {
                    prdDbg($"CsvDataSource: Loading '{Path.GetFileName(filePath)}' (isLoaded={_isLoaded})");
                    LoadData();
                    _lastFileModified = currentModified;
                    _lastLoadTime = DateTime.Now;
                    _isLoaded = true;
                }
            }
        }

        private void LoadData()
        {
            _data.Clear();
            _rows.Clear();

            using var parser = new TextFieldParser(FilePath);
            parser.CommentTokens = new[] { "#" };
            parser.SetDelimiters(new[] { ";" });
            parser.HasFieldsEnclosedInQuotes = false;

            // Skip header row
            if (!parser.EndOfData)
            {
                string[]? headerFields = parser.ReadFields();
                if (headerFields != null)
                {
                    // Validate columns match expected
                    ValidateColumns(headerFields);
                }
            }

            while (!parser.EndOfData)
            {
                string[]? fields = parser.ReadFields();
                if (fields == null) continue;

                _rows.Add(fields);

                // Index by all columns for flexible lookups
                for (int keyColIdx = 0; keyColIdx < fields.Length && keyColIdx < ColumnNames.Length; keyColIdx++)
                {
                    string keyValue = fields[keyColIdx];
                    if (string.IsNullOrEmpty(keyValue)) continue;

                    if (!_data.ContainsKey(keyColIdx))
                    {
                        _data[keyColIdx] = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
                    }

                    if (!_data[keyColIdx].ContainsKey(keyValue))
                    {
                        _data[keyColIdx][keyValue] = new Dictionary<int, string>();
                    }

                    // Store all column values for this row
                    for (int colIdx = 0; colIdx < fields.Length && colIdx < ColumnNames.Length; colIdx++)
                    {
                        _data[keyColIdx][keyValue][colIdx] = fields[colIdx];
                    }
                }
            }
        }

        private void ValidateColumns(string[] headerFields)
        {
            var expectedSet = new HashSet<string>(ColumnNames, StringComparer.OrdinalIgnoreCase);
            var actualSet = new HashSet<string>(headerFields, StringComparer.OrdinalIgnoreCase);

            // Check for extra columns in file that aren't in the class
            var extraColumns = actualSet.Except(expectedSet).ToList();
            if (extraColumns.Count > 0)
            {
                prdDbg($"Warning: CSV file '{Path.GetFileName(FilePath)}' contains columns not defined in class: {string.Join(", ", extraColumns)}");
            }

            // Check for missing columns that are expected
            var missingColumns = expectedSet.Except(actualSet).ToList();
            if (missingColumns.Count > 0)
            {
                prdDbg($"Warning: CSV file '{Path.GetFileName(FilePath)}' is missing expected columns: {string.Join(", ", missingColumns)}");
            }
        }

        /// <summary>
        /// Gets a value from the CSV using the default key column.
        /// </summary>
        /// <param name="keyValue">The value to look up in the key column.</param>
        /// <param name="columnIndex">The column index to retrieve.</param>
        /// <returns>The value, or null if not found.</returns>
        protected string? GetValue(string keyValue, int columnIndex)
        {
            return GetValue(keyValue, columnIndex, DefaultKeyColumnIndex);
        }

        /// <summary>
        /// Gets a value from the CSV using a specific key column.
        /// </summary>
        /// <param name="keyValue">The value to look up in the key column.</param>
        /// <param name="columnIndex">The column index to retrieve.</param>
        /// <param name="keyColumnIndex">The column index to use as the key.</param>
        /// <returns>The value, or null if not found.</returns>
        protected string? GetValue(string keyValue, int columnIndex, int keyColumnIndex)
        {
            EnsureLoaded();

            if (!_data.TryGetValue(keyColumnIndex, out var keyDict))
                return null;

            if (!keyDict.TryGetValue(keyValue, out var rowData))
                return null;

            if (!rowData.TryGetValue(columnIndex, out var value))
                return null;

            return value;
        }

        /// <summary>
        /// Checks if a key exists in the default key column.
        /// </summary>
        protected bool ContainsKey(string keyValue)
        {
            return ContainsKey(keyValue, DefaultKeyColumnIndex);
        }

        /// <summary>
        /// Checks if a key exists in a specific key column.
        /// </summary>
        protected bool ContainsKey(string keyValue, int keyColumnIndex)
        {
            EnsureLoaded();

            if (!_data.TryGetValue(keyColumnIndex, out var keyDict))
                return false;

            return keyDict.ContainsKey(keyValue);
        }

        /// <summary>
        /// Gets all unique values from a specific column.
        /// </summary>
        protected IEnumerable<string> GetAllValuesInColumn(int columnIndex)
        {
            EnsureLoaded();
            return _rows
                .Where(r => r.Length > columnIndex)
                .Select(r => r[columnIndex])
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct();
        }

        /// <summary>
        /// Gets all keys in the default key column.
        /// </summary>
        protected IEnumerable<string> GetAllKeys()
        {
            return GetAllKeys(DefaultKeyColumnIndex);
        }

        /// <summary>
        /// Gets all keys in a specific key column.
        /// </summary>
        protected IEnumerable<string> GetAllKeys(int keyColumnIndex)
        {
            EnsureLoaded();

            if (!_data.TryGetValue(keyColumnIndex, out var keyDict))
                return Enumerable.Empty<string>();

            return keyDict.Keys;
        }

        /// <summary>
        /// Forces a reload of the data on next access.
        /// </summary>
        public void Invalidate()
        {
            lock (_lock)
            {
                _isLoaded = false;
            }
        }

        /// <summary>
        /// Gets a value by key and column name (string).
        /// Used for dynamic column access at runtime.
        /// </summary>
        /// <param name="keyValue">The value to look up in the key column.</param>
        /// <param name="columnName">The column name to retrieve.</param>
        /// <param name="keyColumnIndex">The column index to use as the key (default 0).</param>
        /// <returns>The value, or null if not found.</returns>
        public string? GetByColumnName(string keyValue, string columnName, int keyColumnIndex = 0)
        {
            EnsureLoaded();

            // Find column index by name
            int columnIndex = -1;
            for (int i = 0; i < ColumnNames.Length; i++)
            {
                if (ColumnNames[i].Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    columnIndex = i;
                    break;
                }
            }

            if (columnIndex < 0)
                return null;

            return GetValue(keyValue, columnIndex, keyColumnIndex);
        }

        /// <summary>
        /// Gets a column value from a row array using an enum column index.
        /// Use when iterating Rows to avoid (int) casts everywhere.
        /// </summary>
        /// <typeparam name="TColumn">The column enum type.</typeparam>
        /// <param name="row">The row array.</param>
        /// <param name="column">The column enum value.</param>
        /// <returns>The column value, or empty string if out of bounds.</returns>
        public static string Col<TColumn>(string[] row, TColumn column) where TColumn : Enum
        {
            int index = Convert.ToInt32(column);
            return row.Length > index ? row[index] : string.Empty;
        }
    }
}
