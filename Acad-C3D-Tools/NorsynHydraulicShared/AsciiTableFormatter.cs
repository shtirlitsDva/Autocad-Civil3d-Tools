using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NorsynHydraulicCalc
{
    internal static class AsciiTableFormatter
    {
        public static string CreateAsciiTableColumns(List<(string, List<object>)> columns, List<string> rowNames, string doubleFormat)
        {
            // Ensure that rowNames match the number of rows
            if (rowNames.Count != columns.First().Item2.Count)
                throw new ArgumentException("Row names count must match the number of rows in the columns.");

            // Determine the maximum width for each column and for the row names
            int rowNameWidth = rowNames.Max(name => name.Length);
            var columnWidths = columns.Select(col =>
                Math.Max(col.Item1.Length, col.Item2.Max(val => Format(val, doubleFormat).Length))).ToList();

            // Generate table header with row name column
            string header = "| " + "Name".PadLeft(rowNameWidth) + " | " +
                            string.Join(" | ", columns.Select((col, idx) => col.Item1.PadLeft(columnWidths[idx]))) + " |";

            // Generate separator line
            string separator = "+-" + new string('-', rowNameWidth) + "-+-" +
                               string.Join("-+-", columnWidths.Select(width => new string('-', width))) + "-+";

            // Generate table rows
            int numRows = columns.First().Item2.Count;
            List<string> rows = new List<string>();
            for (int i = 0; i < numRows; i++)
            {
                string row = "| " + rowNames[i].PadLeft(rowNameWidth) + " | " +
                             string.Join(" | ", columns.Select((col, idx) => Format(col.Item2[i], doubleFormat).PadLeft(columnWidths[idx]))) + " |";
                rows.Add(row);
            }

            // Combine all parts into the final table
            return separator + "\n" + header + "\n" + separator + "\n" + string.Join("\n", rows) + "\n" + separator;
        }

        // Local formatter
        private static string Format(object? val, string doubleFormat)
        {
            return val switch
            {
                null => "",
                string s => s,
                double d => d.ToString(doubleFormat),
                float f => f.ToString(doubleFormat),
                _ => val.ToString() ?? ""
            };
        }

        public static string CreateAsciiTableRows(
            string tableName,
            List<(string, List<object>)> rows,
            List<string> columnNames,
            List<string> units,
            string format)
        {
            // Ensure that each row has the same number of data points as there are column names
            if (rows.Any(row => row.Item2.Count != columnNames.Count))
                throw new ArgumentException("All rows must have the same number of data points as there are column names.");

            if (units.Count != columnNames.Count)
                throw new ArgumentException("Units list must have the same number of elements as columnNames.");

            var splitColumnNames = columnNames.Select(name => name.Split('\n')).ToList();
            var maxColumnNameLines = splitColumnNames.Max(lines => lines.Length);

            int rowNameWidth = Math.Max("Row Names".Length, rows.Max(row => row.Item1.Length));
            var columnWidths = new List<int>();

            for (int i = 0; i < columnNames.Count; i++)
            {
                int maxNameWidth = splitColumnNames[i].Max(line => line.Length);
                int unitWidth = units[i].Length;
                int maxDataWidth = rows.Max(row => Format(row.Item2[i], format).Length);
                int columnWidth = new[] { maxNameWidth, unitWidth, maxDataWidth }.Max();
                columnWidths.Add(columnWidth);
            }

            int tableWidth = 3 + rowNameWidth + columnWidths.Sum() + (columnWidths.Count * 3);
            string tableTitle = tableName.PadLeft((tableWidth + tableName.Length) / 2).PadRight(tableWidth);

            string separator = "+-" + new string('-', rowNameWidth) + "-+-" +
                               string.Join("-+-", columnWidths.Select(width => new string('-', width))) + "-+";

            List<string> headerLines = new List<string>();
            for (int lineIndex = 0; lineIndex < maxColumnNameLines; lineIndex++)
            {
                string headerLine = "| " + (lineIndex == 0 ? "Row Names".PadLeft(rowNameWidth) : new string(' ', rowNameWidth)) + " | ";
                for (int colIndex = 0; colIndex < columnNames.Count; colIndex++)
                {
                    var nameLines = splitColumnNames[colIndex];
                    string lineText = lineIndex < nameLines.Length ? nameLines[lineIndex] : "";
                    headerLine += lineText.PadLeft(columnWidths[colIndex]) + " | ";
                }
                headerLines.Add(headerLine.TrimEnd());
            }

            string unitsLine = "| " + new string(' ', rowNameWidth) + " | " +
                               string.Join(" | ", units.Select((unit, idx) => unit.PadLeft(columnWidths[idx]))) + " |";

            string headerSeparator = "+=" + new string('=', rowNameWidth) + "=+=" +
                                     string.Join("=+=", columnWidths.Select(width => new string('=', width))) + "=+";

            List<string> tableRows = new List<string>();
            foreach (var row in rows)
            {
                string rowString = "| " + row.Item1.PadLeft(rowNameWidth) + " | " +
                                   string.Join(" | ", row.Item2.Select((val, idx) => Format(val, format).PadLeft(columnWidths[idx]))) + " |";
                tableRows.Add(rowString);
            }

            var result = new List<string>
            {
                tableTitle,
                separator
            };

            result.AddRange(headerLines);
            result.Add(unitsLine);
            result.Add(headerSeparator);
            result.AddRange(tableRows);
            result.Add(separator);

            return string.Join("\n", result);
        }
    }
}