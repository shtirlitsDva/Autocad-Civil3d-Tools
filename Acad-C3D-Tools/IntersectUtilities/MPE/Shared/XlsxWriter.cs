using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace IntersectUtilities.MPE.Shared
{
    // Minimal dependency-free .xlsx writer (OOXML parts written straight into a ZipArchive).
    // Ported from the working implementation in MPE/MatchBBR/MatchBBR.cs. MatchBBR keeps its own
    // copy on purpose: its writer is coupled to the read-modify-write dictionary shape it needs,
    // while this one takes plain rows and additionally emits real numeric cells so the result can
    // be sorted and filtered in Excel.
    internal static class XlsxWriter
    {
        public static void Write(
            string outputPath,
            string sheetName,
            IReadOnlyList<string> headers,
            IReadOnlyList<IReadOnlyList<object?>> rows)
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            using FileStream fileStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

            CreateZipEntry(
                archive,
                "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" "
                + "ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + "<Override PartName=\"/xl/worksheets/sheet1.xml\" "
                + "ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
                + "</Types>");

            CreateZipEntry(
                archive,
                "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" "
                + "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" "
                + "Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");

            CreateZipEntry(
                archive,
                "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
                + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                + $"<sheets><sheet name=\"{EscapeXml(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>"
                + "</workbook>");

            CreateZipEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" "
                + "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" "
                + "Target=\"worksheets/sheet1.xml\"/>"
                + "</Relationships>");

            CreateZipEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(headers, rows));
        }

        // Several sheets with cells at explicit addresses, so a sheet can mirror the layout of a template
        // (AUTOAMKB writes its rows from B14 down, exactly where the AMK templates expect them).
        public static void WriteWorkbook(string outputPath, IReadOnlyList<XlsxWorksheet> sheets)
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            using FileStream fileStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

            StringBuilder contentTypes = new StringBuilder();
            contentTypes.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            contentTypes.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            contentTypes.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            contentTypes.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            contentTypes.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            for (int i = 0; i < sheets.Count; i++)
            {
                contentTypes.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ");
                contentTypes.Append("ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            contentTypes.Append("</Types>");
            CreateZipEntry(archive, "[Content_Types].xml", contentTypes.ToString());

            CreateZipEntry(
                archive,
                "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" "
                + "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" "
                + "Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");

            StringBuilder workbook = new StringBuilder();
            workbook.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            workbook.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
            workbook.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            StringBuilder relationships = new StringBuilder();
            relationships.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            relationships.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 0; i < sheets.Count; i++)
            {
                workbook.Append($"<sheet name=\"{EscapeXml(sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
                relationships.Append($"<Relationship Id=\"rId{i + 1}\" ");
                relationships.Append("Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" ");
                relationships.Append($"Target=\"worksheets/sheet{i + 1}.xml\"/>");
            }
            workbook.Append("</sheets></workbook>");
            relationships.Append("</Relationships>");
            CreateZipEntry(archive, "xl/workbook.xml", workbook.ToString());
            CreateZipEntry(archive, "xl/_rels/workbook.xml.rels", relationships.ToString());

            for (int i = 0; i < sheets.Count; i++)
            {
                CreateZipEntry(archive, $"xl/worksheets/sheet{i + 1}.xml", BuildAddressedSheetXml(sheets[i]));
            }
        }

        private static string BuildAddressedSheetXml(XlsxWorksheet sheet)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            if (sheet.ColumnWidths.Count > 0)
            {
                builder.Append("<cols>");
                foreach (XlsxColumnWidth column in sheet.ColumnWidths.OrderBy(column => column.Column))
                {
                    string width = column.Width.ToString("0.##", CultureInfo.InvariantCulture);
                    builder.Append($"<col min=\"{column.Column}\" max=\"{column.Column}\" width=\"{width}\" customWidth=\"1\"/>");
                }
                builder.Append("</cols>");
            }

            builder.Append("<sheetData>");
            foreach (IGrouping<int, XlsxCell> row in sheet.Cells.GroupBy(cell => cell.Row).OrderBy(row => row.Key))
            {
                builder.Append($"<row r=\"{row.Key}\">");
                foreach (XlsxCell cell in row.OrderBy(cell => cell.Column))
                {
                    builder.Append(BuildAddressedCellXml($"{GetColumnName(cell.Column - 1)}{cell.Row}", cell.Value));
                }
                builder.Append("</row>");
            }
            builder.Append("</sheetData>");
            builder.Append("</worksheet>");
            return builder.ToString();
        }

        private static string BuildAddressedCellXml(string cellReference, XlsxValue value) =>
            value.Match(
                text => text.Length == 0
                    ? string.Empty
                    : $"<c r=\"{cellReference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{EscapeXml(text)}</t></is></c>",
                number => double.IsFinite(number)
                    ? $"<c r=\"{cellReference}\"><v>{number.ToString("R", CultureInfo.InvariantCulture)}</v></c>"
                    : string.Empty);

        private static string BuildWorksheetXml(
            IReadOnlyList<string> headers,
            IReadOnlyList<IReadOnlyList<object?>> rows)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            builder.Append("<sheetData>");

            builder.Append("<row r=\"1\">");
            for (int i = 0; i < headers.Count; i++)
            {
                builder.Append(BuildInlineStringCellXml($"{GetColumnName(i)}1", headers[i]));
            }
            builder.Append("</row>");

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                int rowNumber = rowIndex + 2;
                IReadOnlyList<object?> row = rows[rowIndex];
                builder.Append($"<row r=\"{rowNumber}\">");
                for (int i = 0; i < row.Count; i++)
                {
                    builder.Append(BuildCellXml($"{GetColumnName(i)}{rowNumber}", row[i]));
                }
                builder.Append("</row>");
            }

            builder.Append("</sheetData>");
            builder.Append("</worksheet>");
            return builder.ToString();
        }

        private static string BuildCellXml(string cellReference, object? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            // Numeric cells are emitted untyped so Excel stores them as numbers, which keeps
            // sorting and conditional formatting on the difference column meaningful.
            switch (value)
            {
                case double doubleValue:
                    if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                    {
                        return BuildInlineStringCellXml(cellReference, doubleValue.ToString(CultureInfo.InvariantCulture));
                    }
                    return $"<c r=\"{cellReference}\"><v>{doubleValue.ToString("R", CultureInfo.InvariantCulture)}</v></c>";
                case int intValue:
                    return $"<c r=\"{cellReference}\"><v>{intValue.ToString(CultureInfo.InvariantCulture)}</v></c>";
                default:
                    string text = value.ToString() ?? string.Empty;
                    return text.Length == 0
                        ? string.Empty
                        : BuildInlineStringCellXml(cellReference, text);
            }
        }

        private static string BuildInlineStringCellXml(string cellReference, string value)
        {
            return $"<c r=\"{cellReference}\" t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";
        }

        private static string GetColumnName(int zeroBasedIndex)
        {
            StringBuilder builder = new StringBuilder();
            int index = zeroBasedIndex;
            while (true)
            {
                builder.Insert(0, (char)('A' + (index % 26)));
                index = (index / 26) - 1;
                if (index < 0)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        private static string EscapeXml(string value)
        {
            return SecurityElement.Escape(value) ?? string.Empty;
        }

        private static void CreateZipEntry(ZipArchive archive, string path, string contents)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            using StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(contents);
        }
    }

    /// <summary>A cell value for <see cref="XlsxWriter.WriteWorkbook"/>: text, or a number Excel can sort and sum.</summary>
    internal abstract record XlsxValue
    {
        private XlsxValue() { }

        public abstract TOut Match<TOut>(Func<string, TOut> text, Func<double, TOut> number);

        public static XlsxValue Of(string text) => new Text(text);

        public static XlsxValue Of(double number) => new Number(number);

        internal sealed record Text(string Value) : XlsxValue
        {
            public override TOut Match<TOut>(Func<string, TOut> text, Func<double, TOut> number) => text(Value);
        }

        internal sealed record Number(double Value) : XlsxValue
        {
            public override TOut Match<TOut>(Func<string, TOut> text, Func<double, TOut> number) => number(Value);
        }
    }

    /// <summary>A cell at a 1-based row and column.</summary>
    internal sealed record XlsxCell(int Row, int Column, XlsxValue Value);

    internal sealed record XlsxColumnWidth(int Column, double Width);

    internal sealed record XlsxWorksheet(string Name, IReadOnlyList<XlsxCell> Cells, IReadOnlyList<XlsxColumnWidth> ColumnWidths);
}
