using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace IntersectUtilities.MPE.Shared
{
    // Minimal dependency-free .xlsx reader (OOXML parts read straight out of a ZipArchive).
    // Lifted from the original MPE/MatchBBR/MatchBBR.cs implementation and generalized on two
    // points that the single-worksheet, fixed-column original could not express:
    //   1. any worksheet can be addressed by name, not just the first one in the workbook;
    //   2. numeric cells can be read as doubles. The raw <v> payload of a numeric cell is always
    //      an invariant-culture decimal regardless of the machine locale, so parsing it with the
    //      current culture silently fails on a da-DK box — hence the explicit InvariantCulture.
    // Companion to XlsxWriter in this folder; the two share no code on purpose (reading needs the
    // relationship graph, writing does not).
    internal static class XlsxReader
    {
        private static readonly XNamespace SpreadsheetMlNs =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace PackageRelationshipsNs =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace OfficeDocumentRelationshipsNs =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        // Worksheet names in workbook order. Cheap: only workbook.xml is parsed.
        public static IReadOnlyList<string> ListSheetNames(string excelPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(excelPath);
            XDocument workbookDocument = XDocument.Parse(ReadZipEntryText(archive, "xl/workbook.xml"));

            return workbookDocument
                .Descendants(SpreadsheetMlNs + "sheet")
                .Select(x => (string?)x.Attribute("name") ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        // Reads one worksheet. Passing null reads the first sheet in workbook order, which is what
        // the old MatchBBR behaviour was.
        public static XlsxSheet Read(string excelPath, string? sheetName = null)
        {
            using ZipArchive archive = ZipFile.OpenRead(excelPath);

            Dictionary<string, string> sharedStrings = LoadSharedStrings(archive);
            XDocument workbookDocument = XDocument.Parse(ReadZipEntryText(archive, "xl/workbook.xml"));

            (string resolvedName, string worksheetPath) = ResolveWorksheet(archive, workbookDocument, sheetName);
            XDocument worksheetDocument = XDocument.Parse(ReadZipEntryText(archive, worksheetPath));

            Dictionary<string, Dictionary<string, string>> rows = LoadWorksheetRows(worksheetDocument, sharedStrings);
            if (rows.Count == 0)
            {
                return XlsxSheet.Empty(resolvedName);
            }

            // The first row present in the sheet is the header row. Sheets that start below row 1
            // are handled because ordering is by the row's own "r" reference, not by document order.
            KeyValuePair<string, Dictionary<string, string>> headerEntry = rows
                .OrderBy(kvp => GetRowNumber(kvp.Key))
                .First();
            if (headerEntry.Value.Count == 0)
            {
                return XlsxSheet.Empty(resolvedName);
            }

            List<string> orderedColumns = headerEntry.Value.Keys.OrderBy(GetColumnIndex).ToList();
            Dictionary<string, string> headerByColumn = orderedColumns.ToDictionary(
                column => column,
                column => GetCellValue(headerEntry.Value, column),
                StringComparer.OrdinalIgnoreCase);

            List<XlsxRow> dataRows = rows
                .OrderBy(kvp => GetRowNumber(kvp.Key))
                .Skip(1)
                .Select(kvp => new XlsxRow(kvp.Key, CopyRowValuesForColumns(kvp.Value, orderedColumns)))
                .ToList();

            return new XlsxSheet(resolvedName, orderedColumns, headerByColumn, dataRows);
        }

        // ---- Column-letter helpers, public because callers address cells by column letter ----

        // "AB12" -> "AB". Returns empty for a malformed reference.
        public static string GetColumnName(string? cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return string.Empty;
            }

            int index = 0;
            while (index < cellReference!.Length && char.IsLetter(cellReference[index]))
            {
                index++;
            }

            return cellReference.Substring(0, index).ToUpperInvariant();
        }

        // "A" -> 1, "Z" -> 26, "AA" -> 27. Used only for ordering, so 0 for garbage is fine.
        public static int GetColumnIndex(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return 0;
            }

            int result = 0;
            foreach (char character in columnName.ToUpperInvariant())
            {
                if (character < 'A' || character > 'Z')
                {
                    return result;
                }

                result = (result * 26) + (character - 'A' + 1);
            }

            return result;
        }

        // ---- Internals ----

        private static (string SheetName, string WorksheetPath) ResolveWorksheet(
            ZipArchive archive,
            XDocument workbookDocument,
            string? requestedSheetName)
        {
            List<XElement> sheets = workbookDocument.Descendants(SpreadsheetMlNs + "sheet").ToList();
            if (sheets.Count == 0)
            {
                throw new InvalidOperationException("No worksheets were found in the workbook.");
            }

            XElement sheet = string.IsNullOrWhiteSpace(requestedSheetName)
                ? sheets[0]
                : sheets.FirstOrDefault(x => string.Equals(
                      (string?)x.Attribute("name"),
                      requestedSheetName,
                      StringComparison.OrdinalIgnoreCase))
                  ?? throw new InvalidOperationException(
                      $"Worksheet '{requestedSheetName}' was not found in the workbook.");

            string sheetName = (string?)sheet.Attribute("name") ?? string.Empty;

            string? relationshipId = (string?)sheet.Attribute(OfficeDocumentRelationshipsNs + "id");
            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                throw new InvalidOperationException(
                    $"The relationship for worksheet '{sheetName}' could not be resolved.");
            }

            XDocument workbookRelationships =
                XDocument.Parse(ReadZipEntryText(archive, "xl/_rels/workbook.xml.rels"));
            XElement relationship = workbookRelationships
                .Descendants(PackageRelationshipsNs + "Relationship")
                .FirstOrDefault(x => string.Equals(
                    (string?)x.Attribute("Id"),
                    relationshipId,
                    StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"The target for worksheet '{sheetName}' could not be resolved.");

            string target = (string?)relationship.Attribute("Target")
                ?? throw new InvalidOperationException(
                    $"The target path for worksheet '{sheetName}' was missing.");

            return (sheetName, NormalizeWorkbookRelativePath(target));
        }

        private static string NormalizeWorkbookRelativePath(string target)
        {
            string normalized = target.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                return normalized.TrimStart('/');
            }

            return $"xl/{normalized.TrimStart('/')}";
        }

        private static Dictionary<string, string> LoadSharedStrings(ZipArchive archive)
        {
            if (archive.GetEntry("xl/sharedStrings.xml") is null)
            {
                return new Dictionary<string, string>();
            }

            XDocument document = XDocument.Parse(ReadZipEntryText(archive, "xl/sharedStrings.xml"));
            Dictionary<string, string> result = new Dictionary<string, string>();
            int index = 0;
            foreach (XElement sharedString in document.Descendants(SpreadsheetMlNs + "si"))
            {
                // A shared string can be split across several <t> runs when parts of it are
                // formatted differently; concatenating them recovers the displayed text.
                result[index.ToString(CultureInfo.InvariantCulture)] = string.Concat(
                    sharedString.Descendants(SpreadsheetMlNs + "t").Select(x => x.Value));
                index++;
            }

            return result;
        }

        private static Dictionary<string, Dictionary<string, string>> LoadWorksheetRows(
            XDocument worksheetDocument,
            IReadOnlyDictionary<string, string> sharedStrings)
        {
            Dictionary<string, Dictionary<string, string>> result =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement rowElement in worksheetDocument.Descendants(SpreadsheetMlNs + "row"))
            {
                string rowKey = (string?)rowElement.Attribute("r") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rowKey))
                {
                    continue;
                }

                Dictionary<string, string> rowValues =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (XElement cellElement in rowElement.Elements(SpreadsheetMlNs + "c"))
                {
                    string columnName = GetColumnName((string?)cellElement.Attribute("r"));
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        continue;
                    }

                    rowValues[columnName] = ReadCellValue(cellElement, sharedStrings);
                }

                result[rowKey] = rowValues;
            }

            return result;
        }

        private static string ReadCellValue(
            XElement cellElement,
            IReadOnlyDictionary<string, string> sharedStrings)
        {
            string? cellType = (string?)cellElement.Attribute("t");
            if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(cellElement.Descendants(SpreadsheetMlNs + "t").Select(x => x.Value));
            }

            XElement? valueElement = cellElement.Element(SpreadsheetMlNs + "v");
            if (valueElement is null)
            {
                return string.Empty;
            }

            string rawValue = valueElement.Value;
            if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase)
                && sharedStrings.TryGetValue(rawValue, out string? sharedStringValue))
            {
                return sharedStringValue;
            }

            return rawValue;
        }

        private static string ReadZipEntryText(ZipArchive archive, string entryPath)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryPath)
                ?? throw new InvalidOperationException($"The workbook part '{entryPath}' was not found.");

            using Stream stream = entry.Open();
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static Dictionary<string, string> CopyRowValuesForColumns(
            IReadOnlyDictionary<string, string> source,
            IEnumerable<string> columns)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string column in columns)
            {
                result[column] = GetCellValue(source, column);
            }

            return result;
        }

        private static string GetCellValue(IReadOnlyDictionary<string, string> row, string columnName)
        {
            return row.TryGetValue(columnName, out string? value) ? value : string.Empty;
        }

        private static int GetRowNumber(string rowReference)
        {
            return int.TryParse(rowReference, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : 0;
        }
    }

    internal sealed class XlsxSheet
    {
        public XlsxSheet(
            string name,
            IReadOnlyList<string> columnOrder,
            IReadOnlyDictionary<string, string> headerByColumn,
            IReadOnlyList<XlsxRow> rows)
        {
            Name = name;
            ColumnOrder = columnOrder;
            HeaderByColumn = headerByColumn;
            Rows = rows;
        }

        public static XlsxSheet Empty(string name) =>
            new XlsxSheet(
                name,
                new List<string>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new List<XlsxRow>());

        public string Name { get; }

        // Column letters ("A", "B", "AA") in left-to-right order, taken from the header row.
        public IReadOnlyList<string> ColumnOrder { get; }

        // Column letter -> header text. Blank when the header cell is empty.
        public IReadOnlyDictionary<string, string> HeaderByColumn { get; }

        public IReadOnlyList<XlsxRow> Rows { get; }

        public bool IsEmpty => ColumnOrder.Count == 0 || Rows.Count == 0;

        // First column whose header matches the predicate, or null. Used to auto-detect
        // "Vejnavn" / "Husnummer" / "Varmedistrikt" style columns without hard-coding letters.
        public string? FindColumnByHeader(Func<string, bool> headerPredicate)
        {
            foreach (string column in ColumnOrder)
            {
                if (HeaderByColumn.TryGetValue(column, out string? header)
                    && !string.IsNullOrWhiteSpace(header)
                    && headerPredicate(header))
                {
                    return column;
                }
            }

            return null;
        }

        // "Vejnavn (D)" for display in column pickers — the letter disambiguates duplicate or
        // blank headers, which real workbooks do contain.
        public string DescribeColumn(string column)
        {
            string header = HeaderByColumn.TryGetValue(column, out string? value) ? value : string.Empty;
            return string.IsNullOrWhiteSpace(header) ? $"({column})" : $"{header} ({column})";
        }
    }

    internal sealed class XlsxRow
    {
        public XlsxRow(string rowReference, IReadOnlyDictionary<string, string> cellsByColumn)
        {
            RowReference = rowReference;
            CellsByColumn = cellsByColumn;
        }

        // The sheet's own row number as text, so an export can point back at the source row.
        public string RowReference { get; }

        public IReadOnlyDictionary<string, string> CellsByColumn { get; }

        public string this[string column] =>
            CellsByColumn.TryGetValue(column, out string? value) ? value : string.Empty;

        // Numeric cells arrive as invariant decimals; cells the user typed as text may carry a
        // Danish comma separator, so both are accepted. Thousand separators are not, deliberately:
        // "1.234" is far more likely to be 1.234 than 1234 in this data.
        public bool TryGetDouble(string column, out double value)
        {
            value = 0.0;
            string raw = this[column];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            raw = raw.Trim();
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return double.TryParse(
                raw.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
