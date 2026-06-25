using System.Collections.Generic;
using System.Linq;

using ClosedXML.Excel;

using NorsynDistrictZones.Pricing;

namespace NorsynDistrictZones.Export;

/// <summary>One zone's data for the breakdown workbook: identity, fill colour, priced rows.</summary>
public sealed record ZoneBreakdown(
    int Number, string Name, int ColorArgb,
    IReadOnlyList<BreakdownRow> Rows, double Total, bool Provisional);

/// <summary>
/// Writes the per-area price &amp; length breakdown to a styled .xlsx (ClosedXML): one worksheet
/// with a coloured table per zone stacked top-to-bottom, each with a zone subtotal, ending in a
/// grand total. Pure data in, file out — no AutoCAD dependency. Numbers are stored as values with
/// a number format (length 2-dp, money 0-dp); the user's Danish Excel renders the dot/comma
/// separators. The grand total equals the sum of the zone totals (same source as the labels).
/// </summary>
public static class PriceBreakdownWorkbook
{
    private static readonly string[] Headers =
        { "Rørdimension", "Længde [m]", "Rørpris [kr]", "Antal stik", "Stikpris [kr]", "I alt [kr]" };

    private const string LenFmt = "#,##0.00";
    private const string MoneyFmt = "#,##0";
    private const string CountFmt = "#,##0";

    // Palette (cool slate, readable on white).
    private static readonly XLColor HeaderFill = XLColor.FromArgb(58, 74, 90);    // dark slate
    private static readonly XLColor BandFill = XLColor.FromArgb(238, 242, 246);   // light row tint
    private static readonly XLColor SubtotalFill = XLColor.FromArgb(214, 222, 230);
    private static readonly XLColor GrandFill = XLColor.FromArgb(74, 107, 138);   // AccentPrimary
    private static readonly XLColor GridLine = XLColor.FromArgb(176, 186, 196);

    public static void Save(string path, IReadOnlyList<ZoneBreakdown> zones, double grandTotal, string catalogName)
    {
        using var wb = new XLWorkbook();
        IXLWorksheet ws = wb.Worksheets.Add("Prisoverslag");

        SetColumnWidths(ws);

        int r = 1;
        ws.Cell(r, 1).Value = $"Prisoverslag pr. område — priskatalog: {catalogName}";
        ws.Range(r, 1, r, 6).Merge();
        ws.Cell(r, 1).Style.Font.SetBold().Font.SetFontSize(14);
        r += 2;

        foreach (ZoneBreakdown z in zones)
            r = WriteZone(ws, r, z);

        WriteGrandTotal(ws, r, zones, grandTotal);

        ws.Column(1).AdjustToContents();
        if (ws.Column(1).Width < 18) ws.Column(1).Width = 18;

        wb.SaveAs(path);
    }

    private static int WriteZone(IXLWorksheet ws, int r, ZoneBreakdown z)
    {
        int top = r;

        // Zone header band, filled with the zone's own model-space colour.
        XLColor band = XLColor.FromArgb(z.ColorArgb);
        string title = string.IsNullOrWhiteSpace(z.Name) ? $"Zone {z.Number}" : $"Zone {z.Number} — {z.Name}";
        if (z.Provisional) title += "   ⚠ ufuldstændige data";
        ws.Range(r, 1, r, 6).Merge();
        ws.Cell(r, 1).Value = title;
        ws.Cell(r, 1).Style.Fill.SetBackgroundColor(band);
        ws.Cell(r, 1).Style.Font.SetBold().Font.SetFontColor(ContrastText(z.ColorArgb)).Font.SetFontSize(12);
        r++;

        // Column header row.
        for (int c = 0; c < Headers.Length; c++)
        {
            IXLCell cell = ws.Cell(r, c + 1);
            cell.Value = Headers[c];
            cell.Style.Fill.SetBackgroundColor(HeaderFill);
            cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
            cell.Style.Alignment.Horizontal = c == 0 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Right;
        }
        r++;

        bool shade = false;
        foreach (BreakdownRow row in z.Rows)
        {
            WriteDataRow(ws, r, row.Dimension, row.Length, row.PipeCost, row.StikCount, row.StikCost, row.Total,
                shade ? BandFill : XLColor.White, bold: false);
            shade = !shade;
            r++;
        }

        // Zone subtotal.
        WriteDataRow(ws, r, "I alt",
            z.Rows.Sum(x => x.Length), z.Rows.Sum(x => x.PipeCost),
            z.Rows.Sum(x => x.StikCount), z.Rows.Sum(x => x.StikCost), z.Total,
            SubtotalFill, bold: true);
        ws.Range(r, 1, r, 6).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        r++;

        // Outline the whole zone table, then a spacer.
        ws.Range(top, 1, r - 1, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(top, 1, r - 1, 6).Style.Border.OutsideBorderColor = GridLine;
        return r + 1;
    }

    private static void WriteGrandTotal(IXLWorksheet ws, int r, IReadOnlyList<ZoneBreakdown> zones, double grandTotal)
    {
        var all = zones.SelectMany(z => z.Rows).ToList();
        WriteDataRow(ws, r, "Alle zoner i alt",
            all.Sum(x => x.Length), all.Sum(x => x.PipeCost),
            all.Sum(x => x.StikCount), all.Sum(x => x.StikCost), grandTotal,
            GrandFill, bold: true);
        IXLRange range = ws.Range(r, 1, r, 6);
        range.Style.Font.SetFontColor(XLColor.White);
        range.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
    }

    private static void WriteDataRow(
        IXLWorksheet ws, int r, string label,
        double length, double pipeCost, int stikCount, double stikCost, double total,
        XLColor fill, bool bold)
    {
        ws.Cell(r, 1).Value = label;
        ws.Cell(r, 2).Value = length;
        ws.Cell(r, 3).Value = pipeCost;
        ws.Cell(r, 4).Value = stikCount;
        ws.Cell(r, 5).Value = stikCost;
        ws.Cell(r, 6).Value = total;

        ws.Cell(r, 2).Style.NumberFormat.Format = LenFmt;
        ws.Cell(r, 3).Style.NumberFormat.Format = MoneyFmt;
        ws.Cell(r, 4).Style.NumberFormat.Format = CountFmt;
        ws.Cell(r, 5).Style.NumberFormat.Format = MoneyFmt;
        ws.Cell(r, 6).Style.NumberFormat.Format = MoneyFmt;

        IXLRange range = ws.Range(r, 1, r, 6);
        range.Style.Fill.SetBackgroundColor(fill);
        if (bold) range.Style.Font.Bold = true;
        else ws.Cell(r, 6).Style.Font.Bold = true; // "I alt" column always bold
    }

    private static void SetColumnWidths(IXLWorksheet ws)
    {
        ws.Column(1).Width = 20;
        ws.Column(2).Width = 14;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 12;
        ws.Column(5).Width = 14;
        ws.Column(6).Width = 16;
    }

    /// <summary>Black or white text for readability over the given ARGB fill (luminance rule).</summary>
    private static XLColor ContrastText(int argb)
    {
        int r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
        double luminance = 0.299 * r + 0.587 * g + 0.114 * b;
        return luminance < 140 ? XLColor.White : XLColor.Black;
    }
}
