using Microsoft.Win32;

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DimensioneringV2.UI.Forbrugere
{
    internal static class ForbrugereExporter
    {
        private static readonly NumberFormatInfo DotFormat = new NumberFormatInfo { NumberDecimalSeparator = "." };
        private static readonly NumberFormatInfo CommaFormat = new NumberFormatInfo { NumberDecimalSeparator = "," };

        private static readonly string[] Headers =
        {
            "Adresse", "Anvendelse", "BBR-areal [m\u00B2]", "Effekt [kW]",
            "\u00C5rsforbrug [MWh]", "Stikl\u00E6ngde [m]", "DN",
            "Tryktab i stikledning [bar]", "N\u00F8dvendigt disponibelt tryk [bar]"
        };

        internal static void ExportToCsv(List<ForbrugerRow> rows, bool useComma = false)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = "Forbrugere",
                Title = "Export Forbrugere til CSV"
            };
            if (sfd.ShowDialog() != true) return;

            var nf = useComma ? CommaFormat : DotFormat;

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", Headers));

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(";",
                    Escape(row.Adresse),
                    Escape(row.Type),
                    row.BBRAreal.ToString("F0", nf),
                    row.Effekt.ToString("F2", nf),
                    row.Aarsforbrug.ToString("F2", nf),
                    row.Stiklaengde.ToString("F2", nf),
                    Escape(row.DN),
                    row.Tryktab.ToString("F4", nf),
                    row.NødvendigtDisponibeltTryk.ToString("F2", nf)));
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
        }

        private static string Escape(string value)
        {
            if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
