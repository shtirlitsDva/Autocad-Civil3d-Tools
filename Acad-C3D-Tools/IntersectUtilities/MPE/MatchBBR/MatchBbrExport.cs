using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IntersectUtilities.MPE.Shared;

namespace IntersectUtilities.MPE.MatchBBR
{
    // Writes the comparison grid to a new workbook.
    //
    // Uses the shared XlsxWriter rather than the writer the old MatchBBR carried, because that one
    // emitted every cell as an inline string. Real numeric cells matter here: the delta column is
    // the one you want to sort and conditional-format once the file is open.
    internal static class MatchBbrExport
    {
        public static string Export(
            string outputPath,
            XlsxSheet sourceSheet,
            IReadOnlyList<CompareRule> valueRules,
            Func<CompareRule, string> labelFor,
            IEnumerable<ComparisonRow> rows,
            string sheetName = "Sammenligning")
        {
            List<CompareRule> rules = valueRules.Where(r => !r.IsKey && r.IsConfigured).ToList();

            List<string> headers = new List<string>
            {
                "Status",
                "Årsag",
                "Adressenøgle",
                "Adresse (BBR)",
                "Distrikt (BBR)",
                "BBR handle",
                "Excel række",
            };

            // The source workbook's own columns are carried through so the export can be used as a
            // working list, not just a report.
            List<string> sourceColumns = sourceSheet.ColumnOrder.ToList();
            foreach (string column in sourceColumns)
            {
                string header = sourceSheet.HeaderByColumn.TryGetValue(column, out string? value)
                    ? value
                    : string.Empty;
                headers.Add(string.IsNullOrWhiteSpace(header) ? column : header);
            }

            foreach (CompareRule rule in rules)
            {
                string label = labelFor(rule);
                headers.Add($"{label} (Excel)");
                headers.Add($"{label} (BBR)");
                headers.Add($"Δ {label}");
            }

            List<IReadOnlyList<object?>> exportRows = new List<IReadOnlyList<object?>>();

            foreach (ComparisonRow row in rows)
            {
                List<object?> cells = new List<object?>
                {
                    StatusText(row.Status),
                    row.Reason,
                    row.Key,
                    row.BbrRecord?.Adresse ?? string.Empty,
                    row.BbrRecord?.Distrikt ?? string.Empty,
                    row.BbrRecord?.Handle.ToString() ?? string.Empty,
                    row.ExcelRow?.Row.RowReference ?? string.Empty,
                };

                foreach (string column in sourceColumns)
                {
                    // Source columns stay text on purpose: coercing them to numbers would mangle
                    // house numbers, postcodes and district codes like "7.28.4".
                    cells.Add(row.ExcelRow is null ? string.Empty : row.ExcelRow.Row[column]);
                }

                foreach (CompareRule rule in rules)
                {
                    RuleCellResult? cell = row.Cell(rule.Id);
                    if (cell is null)
                    {
                        cells.Add(string.Empty);
                        cells.Add(string.Empty);
                        cells.Add(null);
                        continue;
                    }

                    if (rule.Kind == CompareRuleKind.Numeric)
                    {
                        // The unrounded numbers where the comparison produced them; the grid
                        // shows two decimals, but the workbook should carry the real values.
                        cells.Add(cell.ExcelNumber ?? AsNumberOrText(cell.ExcelText));
                        cells.Add(cell.BbrNumber ?? AsNumberOrText(cell.BbrText));
                        cells.Add(cell.Delta);
                    }
                    else
                    {
                        cells.Add(cell.ExcelText);
                        cells.Add(cell.BbrText);
                        cells.Add(cell.IsComparable ? (cell.IsMismatch ? "afviger" : "ens") : string.Empty);
                    }
                }

                exportRows.Add(cells);
            }

            XlsxWriter.Write(outputPath, sheetName, headers, exportRows);
            return outputPath;
        }

        public static string StatusText(MatchStatus status) =>
            status switch
            {
                MatchStatus.Match => "Match",
                MatchStatus.Afvigelse => "Afvigelse",
                MatchStatus.KunIExcel => "Kun i Excel",
                MatchStatus.KunITegning => "Kun i tegning",
                MatchStatus.Dublet => "Dublet",
                _ => status.ToString(),
            };

        private static object? AsNumberOrText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || double.TryParse(
                    trimmed.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return value;
            }

            return text;
        }
    }
}
