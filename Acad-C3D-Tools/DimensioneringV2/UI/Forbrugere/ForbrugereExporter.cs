using ClosedXML.Excel;

using Microsoft.Win32;

using System.Collections.Generic;
using System.Globalization;

namespace DimensioneringV2.UI.Forbrugere
{
    internal static class ForbrugereExporter
    {
        private static readonly CultureInfo DanishCulture = new CultureInfo("da-DK");

        private static readonly string[] Headers =
        {
            "Adresse", "Anvendelse", "BBR-areal [m\u00B2]", "Effekt [kW]",
            "\u00C5rsforbrug [MWh]", "Stikl\u00E6ngde [m]", "DN", 
            "Tryktab i stikledning [bar]", "Nødvendigt disponibelt tryk [bar]"
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
                ws.Cell(r + 2, 9).Value = row.NødvendigtDisponibeltTryk;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(sfd.FileName);
        }

    }
}
