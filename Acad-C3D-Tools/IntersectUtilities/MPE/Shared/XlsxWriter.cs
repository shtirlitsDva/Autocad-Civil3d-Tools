using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
}
