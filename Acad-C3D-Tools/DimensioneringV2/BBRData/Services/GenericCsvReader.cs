using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualBasic.FileIO;

namespace DimensioneringV2.BBRData.Services
{
    internal class CsvLoadResult
    {
        public string[] Headers { get; }
        public List<string[]> Rows { get; }

        public CsvLoadResult(string[] headers, List<string[]> rows)
        {
            Headers = headers;
            Rows = rows;
        }
    }

    internal static class GenericCsvReader
    {
        /// <summary>
        /// Loads a CSV file with configurable delimiter.
        /// Returns null if parsing fails; errorMessage describes the issue.
        /// </summary>
        public static CsvLoadResult? Load(
            string filePath,
            string delimiter,
            out string? errorMessage)
        {
            errorMessage = null;

            if (!File.Exists(filePath))
            {
                errorMessage = $"File not found: {filePath}";
                return null;
            }

            try
            {
                using var parser = new TextFieldParser(filePath);
                parser.CommentTokens = new[] { "#" };
                parser.SetDelimiters(new[] { delimiter });
                parser.HasFieldsEnclosedInQuotes = true;

                // Read header row
                if (parser.EndOfData)
                {
                    errorMessage = "File is empty.";
                    return null;
                }

                string[]? headerFields = parser.ReadFields();
                if (headerFields == null || headerFields.Length == 0)
                {
                    errorMessage = "Could not parse header row.";
                    return null;
                }

                // Trim header names
                var headers = headerFields.Select(h => h.Trim()).ToArray();

                // Check for meaningful data — if we only get 1 column with long content,
                // it likely means the delimiter is wrong
                var rows = new List<string[]>();
                bool hasMultipleColumns = headers.Length > 1;

                while (!parser.EndOfData)
                {
                    string[]? fields = parser.ReadFields();
                    if (fields == null) continue;

                    // Validate column count matches header
                    if (fields.Length != headers.Length)
                    {
                        // Tolerate rows with fewer fields by padding with empty strings
                        if (fields.Length < headers.Length)
                        {
                            var padded = new string[headers.Length];
                            Array.Copy(fields, padded, fields.Length);
                            for (int i = fields.Length; i < headers.Length; i++)
                                padded[i] = string.Empty;
                            fields = padded;
                        }
                        else
                        {
                            // More fields than headers — truncate
                            fields = fields.Take(headers.Length).ToArray();
                        }
                    }

                    rows.Add(fields);
                }

                if (rows.Count == 0)
                {
                    errorMessage = "No data rows found.";
                    return null;
                }

                // Heuristic: if only 1 column detected and rows have long strings,
                // the delimiter is probably wrong
                if (!hasMultipleColumns && rows.Any(r => r[0].Contains(';') || r[0].Contains(',')))
                {
                    errorMessage = "Delimiter error — only one column detected. Try a different delimiter.";
                    return null;
                }

                return new CsvLoadResult(headers, rows);
            }
            catch (Exception ex)
            {
                errorMessage = $"Error reading CSV: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Converts a string value to the specified BBR data type using the given decimal separator.
        /// </summary>
        public static object? ConvertValue(string rawValue, Models.BbrDataType targetType, string decimalSeparator)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            switch (targetType)
            {
                case Models.BbrDataType.String:
                    return rawValue.Trim();

                case Models.BbrDataType.Int:
                    // Remove decimal separator artifacts, try parse
                    var intStr = rawValue.Trim();
                    if (int.TryParse(intStr, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int intVal))
                        return intVal;
                    // Try with dots/commas removed for thousands
                    intStr = intStr.Replace(".", "").Replace(",", "");
                    if (int.TryParse(intStr, out intVal))
                        return intVal;
                    return null;

                case Models.BbrDataType.Double:
                    var dblStr = rawValue.Trim();
                    // Normalize decimal separator to invariant culture dot
                    if (decimalSeparator == ",")
                        dblStr = dblStr.Replace(",", ".");
                    if (double.TryParse(dblStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double dblVal))
                        return dblVal;
                    return null;

                default:
                    return rawValue;
            }
        }
    }
}
