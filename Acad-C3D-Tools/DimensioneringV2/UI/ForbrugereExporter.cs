using ClosedXML.Excel;

using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

using Microsoft.Win32;

using System.Collections.Generic;
using System.Globalization;

namespace DimensioneringV2.UI
{
    internal static class ForbrugereExporter
    {
        private static readonly CultureInfo DanishCulture = new CultureInfo("da-DK");

        private static readonly string[] Headers =
        {
            "Adresse", "Type", "BBR-areal [m\u00B2]", "Effekt [kW]",
            "\u00C5rsforbrug [MWh]", "Stikl\u00E6ngde [m]", "DN", "Tryktab [bar]"
        };

        internal static void ExportToExcel(List<ForbrugerRow> rows)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = "Forbrugere",
                Title = "Export Forbrugere til Excel"
            };
            if (sfd.ShowDialog() != true) return;

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Forbrugere");

            for (int i = 0; i < Headers.Length; i++)
                ws.Cell(1, i + 1).Value = Headers[i];
            ws.Row(1).Style.Font.Bold = true;

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                ws.Cell(r + 2, 1).Value = row.Adresse;
                ws.Cell(r + 2, 2).Value = row.Type;
                ws.Cell(r + 2, 3).Value = row.BBRAreal;
                ws.Cell(r + 2, 4).Value = row.Effekt;
                ws.Cell(r + 2, 5).Value = row.Aarsforbrug;
                ws.Cell(r + 2, 6).Value = row.Stiklaengde;
                ws.Cell(r + 2, 7).Value = row.DN;
                ws.Cell(r + 2, 8).Value = row.Tryktab;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(sfd.FileName);
        }

        internal static void ExportToPdf(List<ForbrugerRow> rows)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = "Forbrugere",
                Title = "Export Forbrugere til PDF"
            };
            if (sfd.ShowDialog() != true) return;

            var document = new Document();
            document.Info.Title = "Forbrugere";

            var section = document.AddSection();
            section.PageSetup.Orientation = Orientation.Landscape;
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.TopMargin = "1.5cm";
            section.PageSetup.BottomMargin = "1.5cm";
            section.PageSetup.LeftMargin = "1.5cm";
            section.PageSetup.RightMargin = "1.5cm";

            var title = section.AddParagraph("Forbrugere");
            title.Format.Font.Size = 16;
            title.Format.Font.Bold = true;
            title.Format.SpaceAfter = "0.5cm";

            var table = section.AddTable();
            table.Borders.Width = 0.25;
            table.Borders.Color = Colors.Gray;
            table.Format.Font.Size = 8;

            // Column widths for landscape A4 (~25.7cm usable)
            double[] widths = { 5.0, 3.5, 2.5, 2.5, 3.0, 2.5, 2.5, 2.5 };
            foreach (var w in widths)
            {
                var col = table.AddColumn($"{w.ToString(CultureInfo.InvariantCulture)}cm");
                col.Format.Alignment = ParagraphAlignment.Left;
            }

            // Right-align numeric columns (index 2-5, 7)
            table.Columns[2].Format.Alignment = ParagraphAlignment.Right;
            table.Columns[3].Format.Alignment = ParagraphAlignment.Right;
            table.Columns[4].Format.Alignment = ParagraphAlignment.Right;
            table.Columns[5].Format.Alignment = ParagraphAlignment.Right;
            table.Columns[7].Format.Alignment = ParagraphAlignment.Right;

            // Header row
            var headerRow = table.AddRow();
            headerRow.HeadingFormat = true;
            headerRow.Format.Font.Bold = true;
            headerRow.Shading.Color = Colors.LightGray;
            for (int i = 0; i < Headers.Length; i++)
                headerRow.Cells[i].AddParagraph(Headers[i]);

            // Data rows
            foreach (var row in rows)
            {
                var r = table.AddRow();
                r.Cells[0].AddParagraph(row.Adresse ?? "");
                r.Cells[1].AddParagraph(row.Type ?? "");
                r.Cells[2].AddParagraph(row.BBRAreal.ToString("N0", DanishCulture));
                r.Cells[3].AddParagraph(row.Effekt.ToString("N2", DanishCulture));
                r.Cells[4].AddParagraph(row.Aarsforbrug.ToString("N2", DanishCulture));
                r.Cells[5].AddParagraph(row.Stiklaengde.ToString("N2", DanishCulture));
                r.Cells[6].AddParagraph(row.DN ?? "");
                r.Cells[7].AddParagraph(row.Tryktab.ToString("N4", DanishCulture));
            }

            var renderer = new PdfDocumentRenderer();
            renderer.Document = document;
            renderer.RenderDocument();
            renderer.PdfDocument.Save(sfd.FileName);
        }
    }
}
