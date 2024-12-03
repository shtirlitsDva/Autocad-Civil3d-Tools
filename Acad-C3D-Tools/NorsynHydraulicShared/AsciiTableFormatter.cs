using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NorsynHydraulicCalc
{
    internal static class AsciiTableFormatter
    {
        public static string CreateAsciiTableColumns(List<(string, List<double>)> columns, List<string> rowNames, string format)
        {
            // Ensure that rowNames match the number of rows
            if (rowNames.Count != columns.First().Item2.Count)
                throw new ArgumentException("Row names count must match the number of rows in the columns.");

            // Determine the maximum width for each column and for the row names
            int rowNameWidth = rowNames.Max(name => name.Length);
            var columnWidths = columns.Select(col =>
                Math.Max(col.Item1.Length, col.Item2.Max(val => val.ToString(format).Length))).ToList();

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
                             string.Join(" | ", columns.Select((col, idx) => col.Item2[i].ToString(format).PadLeft(columnWidths[idx]))) + " |";
                rows.Add(row);
            }

            // Combine all parts into the final table
            return separator + "\n" + header + "\n" + separator + "\n" + string.Join("\n", rows) + "\n" + separator;
        }

        public static string CreateAsciiTableRows(
        string tableName,
        List<(string, List<double>)> rows,
        List<string> columnNames,
        List<string> units,
        string format)
        {
            // Ensure that each row has the same number of data points as there are column names
            if (rows.Any(row => row.Item2.Count != columnNames.Count))
                throw new ArgumentException("All rows must have the same number of data points as there are column names.");

            // Ensure units list matches the number of columns
            if (units.Count != columnNames.Count)
                throw new ArgumentException("Units list must have the same number of elements as columnNames.");

            // Split column names into lines
            var splitColumnNames = columnNames.Select(name => name.Split('\n')).ToList();
            var maxColumnNameLines = splitColumnNames.Max(lines => lines.Length);

            // Determine the maximum width for each column and the row names
            int rowNameWidth = Math.Max("Row Names".Length, rows.Max(row => row.Item1.Length));
            var columnWidths = new List<int>();

            for (int i = 0; i < columnNames.Count; i++)
            {
                // Max width among column name lines
                int maxNameWidth = splitColumnNames[i].Max(line => line.Length);
                // Width of the units
                int unitWidth = units[i].Length;
                // Max width of data values in this column
                int maxDataWidth = rows.Max(row => row.Item2[i].ToString(format).Length);

                // The column width is the maximum among column name lines, units, and data values
                int columnWidth = new[] { maxNameWidth, unitWidth, maxDataWidth }.Max();
                columnWidths.Add(columnWidth);
            }

            // Generate the total table width
            int tableWidth = 3 + rowNameWidth + columnWidths.Sum() + (columnWidths.Count * 3);

            // Generate the table name line, centered
            string tableTitle = tableName.PadLeft((tableWidth + tableName.Length) / 2).PadRight(tableWidth);

            // Generate the separator line
            string separator = "+-" + new string('-', rowNameWidth) + "-+-" +
                               string.Join("-+-", columnWidths.Select(width => new string('-', width))) + "-+";

            // Generate the column name rows (could be multiple lines)
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

                // Remove trailing space and pipe
                headerLine = headerLine.TrimEnd();
                headerLines.Add(headerLine);
            }

            // Generate the units row
            string unitsLine = "| " + new string(' ', rowNameWidth) + " | " +
                               string.Join(" | ", units.Select((unit, idx) => unit.PadLeft(columnWidths[idx]))) + " |";

            // Generate the separator line after headers
            string headerSeparator = "+=" + new string('=', rowNameWidth) + "=+=" +
                                     string.Join("=+=", columnWidths.Select(width => new string('=', width))) + "=+";

            // Generate the table rows
            List<string> tableRows = new List<string>();
            foreach (var row in rows)
            {
                string rowString = "| " + row.Item1.PadLeft(rowNameWidth) + " | " +
                                   string.Join(" | ", row.Item2.Select((val, idx) => val.ToString(format).PadLeft(columnWidths[idx]))) + " |";
                tableRows.Add(rowString);
            }

            // Combine all parts into the final table
            var result = new List<string>();
            result.Add(tableTitle);
            result.Add(separator);
            result.AddRange(headerLines);
            result.Add(unitsLine);
            result.Add(headerSeparator);
            result.AddRange(tableRows);
            result.Add(separator);

            return string.Join("\n", result);
        }
    }
}
