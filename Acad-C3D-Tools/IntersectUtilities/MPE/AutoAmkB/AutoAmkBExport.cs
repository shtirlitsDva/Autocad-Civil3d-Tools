using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using IntersectUtilities.MPE.Shared;

namespace IntersectUtilities.MPE.AutoAmkB;

/// <summary>
/// Writes the AUTOAMKB workbook. The logbook and journal sheets put their rows at the same addresses as the
/// AMK templates (headers in row 13, data from B14, columns B-F), so the block B14:F... pastes straight into
/// a template. Source data sits from column Q, after the template's columns, and is never part of that block.
/// </summary>
internal static class AutoAmkBExport
{
    private const int HeaderRow = 13;
    private const int FirstDataRow = 14;
    private const int SourceColumn = 17; // Q

    private static readonly string[] LogbookHeaders =
    {
        "ID Nr.", "Emne", "Arbejdsmiljøudfordring (fritekst)", "Særligt Farligt Arbejde, hovedkategori",
        "Særligt Farligt Arbejde, underkategori", "Iboende sandsynlighed", "Iboende konsekvens", "Risikovurdering",
        "Løsning og tiltag (fritekst)", "Endelig sandsynlighed", "Endelig konsekvens", "Risikovurdering",
        "Koordinering/Kommunikation (fritekst)", "Ansvarlig",
    };

    private static readonly string[] JournalHeaders =
    {
        "Stationering", "Driftselementer, hovedkategori", "Driftselementer, underkategori", "Arb.miljøudfordringer",
        "Uddybning af arbejdsmiljøudfordring (fritekst)", "Iboende sandsynlighed", "Iboende konsekvens", "Risikovurdering",
        "Løsning og tiltag (fritekst)", "Endelig sandsynlighed", "Endelig konsekvens", "Risikovurdering",
        "Koordinering/Kommunikation (fritekst)", "Ansvarlig",
    };

    private static readonly IReadOnlyList<XlsxColumnWidth> TemplateWidths = new[]
    {
        new XlsxColumnWidth(1, 3), new XlsxColumnWidth(2, 14), new XlsxColumnWidth(3, 34), new XlsxColumnWidth(4, 52),
        new XlsxColumnWidth(5, 44), new XlsxColumnWidth(6, 40), new XlsxColumnWidth(17, 10), new XlsxColumnWidth(18, 11),
        new XlsxColumnWidth(19, 11), new XlsxColumnWidth(20, 26), new XlsxColumnWidth(21, 34), new XlsxColumnWidth(22, 8),
        new XlsxColumnWidth(23, 30), new XlsxColumnWidth(24, 24), new XlsxColumnWidth(25, 18),
    };

    public static Result<string> Write(AmkReport report, string outputPath) =>
        Boundary.Try(
            () =>
            {
                XlsxWriter.WriteWorkbook(outputPath, Sheets(report));
                return outputPath;
            },
            $"Excel-filen kunne ikke skrives. Er den åben i Excel? {outputPath}");

    public static IReadOnlyList<XlsxWorksheet> Sheets(AmkReport report) => new[]
    {
        HitSheet(report),
        CriterionSheet(report),
        JournalSheet(report),
        InfoSheet(report),
    };

    private static XlsxWorksheet HitSheet(AmkReport report)
    {
        List<XlsxCell> cells = Intro(
            "AUTOAMKB – logbog, én række pr. fund",
            CopyHint(report.Hits.Count),
            report);
        cells.AddRange(Headers(LogbookHeaders));
        cells.AddRange(SourceHeaders(new[]
        {
            "Alignment", "Station fra", "Station til", "Kriterie", "Detalje", "Antal", "Ejer", "Kildefil", "Handle", "X", "Y",
        }));

        int row = FirstDataRow;
        foreach (Hit hit in report.Hits)
        {
            HitTexts texts = report.Rules.Rules.TextsFor(hit.Kind);
            cells.AddRange(Row(row,
                StationText.Of(hit.Alignment, hit.Extent),
                AmkTemplate.Fill(texts.Topic, hit.Values),
                AmkTemplate.Fill(texts.Description, hit.Values),
                texts.MainCategory,
                texts.SubCategory));
            cells.AddRange(SourceRow(row,
                XlsxValue.Of(hit.Alignment),
                XlsxValue.Of(Math.Round(hit.Extent.From, 2)),
                XlsxValue.Of(Math.Round(hit.Extent.To, 2)),
                XlsxValue.Of(hit.Kind.Name),
                XlsxValue.Of(hit.Trace.Detail),
                XlsxValue.Of(hit.Trace.Count),
                XlsxValue.Of(hit.Trace.Owner),
                XlsxValue.Of(hit.Trace.Source),
                XlsxValue.Of(hit.Trace.Handle),
                XlsxValue.Of(Math.Round(hit.Trace.X, 3)),
                XlsxValue.Of(Math.Round(hit.Trace.Y, 3))));
            row++;
        }

        return new XlsxWorksheet("Logbog pr. fund", cells, TemplateWidths);
    }

    private static XlsxWorksheet CriterionSheet(AmkReport report)
    {
        List<IGrouping<HitKind, Hit>> byKind = report.Hits
            .GroupBy(hit => hit.Kind)
            .OrderBy(group => HitKind.All.ToList().IndexOf(group.Key))
            .ToList();

        List<XlsxCell> cells = Intro(
            "AUTOAMKB – logbog, én række pr. kriterie",
            CopyHint(byKind.Count),
            report);
        cells.AddRange(Headers(LogbookHeaders));
        cells.AddRange(SourceHeaders(new[] { "Kriterie", "Antal fund" }));

        int row = FirstDataRow;
        foreach (IGrouping<HitKind, Hit> group in byKind)
        {
            HitTexts texts = report.Rules.Rules.TextsFor(group.Key);
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["antal"] = group.Count().ToString(CultureInfo.InvariantCulture),
                ["stationer"] = StationList(group),
            };
            cells.AddRange(Row(row,
                "",
                AmkTemplate.Fill(texts.CriterionTopic, values),
                AmkTemplate.Fill(texts.CriterionDescription, values),
                texts.MainCategory,
                texts.SubCategory));
            cells.AddRange(SourceRow(row, XlsxValue.Of(group.Key.Name), XlsxValue.Of(group.Count())));
            row++;
        }

        return new XlsxWorksheet("Logbog pr. kriterie", cells, TemplateWidths);
    }

    // "001:316, 001:328; 002:010" - stations on one alignment joined by commas, alignments by semicolons.
    private static string StationList(IEnumerable<Hit> hits) =>
        string.Join("; ", hits
            .GroupBy(hit => hit.Alignment, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => string.Join(", ", group
                .OrderBy(hit => hit.Extent.From)
                .Select(hit => StationText.Of(hit.Alignment, hit.Extent)))));

    private static XlsxWorksheet JournalSheet(AmkReport report)
    {
        JournalTexts texts = report.Rules.Rules.Journal;
        int rowCount = report.Valves.Count * texts.SubCategories.Count;

        List<XlsxCell> cells = Intro(
            "AUTOAMKB – journal, ventiler",
            CopyHint(rowCount),
            report);
        cells.AddRange(Headers(JournalHeaders));
        cells.AddRange(SourceHeaders(new[]
        {
            "Alignment", "Station", "Antal ventiler", "Komponenttype", "Blok", "Handle", "X", "Y",
        }));

        int row = FirstDataRow;
        foreach (ValveLocation location in report.Valves)
        {
            string station = location.Alignment.Bind(name => location.Station.Map(value => StationText.Point(name, value))).OrElse("");
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["betegnelse"] = AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.Designation), " / "),
                ["dn"] = AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.Dn), "/"),
                ["system"] = AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.System), "/"),
            };
            string description = AmkTemplate.Fill(texts.Description, values);
            ValveInfo first = location.Valves[0];

            foreach (string subCategory in texts.SubCategories)
            {
                cells.AddRange(Row(row, station, texts.MainCategory, subCategory, "", description));
                cells.AddRange(SourceRow(row,
                    XlsxValue.Of(location.Alignment.OrElse("")),
                    location.Station.Match(value => XlsxValue.Of(Math.Round(value, 2)), () => XlsxValue.Of("")),
                    XlsxValue.Of(location.Valves.Count),
                    XlsxValue.Of(AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.ElementType), ", ")),
                    XlsxValue.Of(AmkTemplate.JoinDistinct(location.Valves.Select(valve => valve.BlockName), ", ")),
                    XlsxValue.Of(string.Join(", ", location.Valves.Select(valve => valve.Handle))),
                    XlsxValue.Of(Math.Round(first.X, 3)),
                    XlsxValue.Of(Math.Round(first.Y, 3))));
                row++;
            }
        }

        return new XlsxWorksheet("Journal", cells, TemplateWidths);
    }

    private static XlsxWorksheet InfoSheet(AmkReport report)
    {
        List<XlsxCell> cells = new List<XlsxCell>();
        int row = 1;

        void Line(params string[] texts)
        {
            for (int i = 0; i < texts.Length; i++) cells.Add(new XlsxCell(row, i + 1, XlsxValue.Of(texts[i])));
            row++;
        }

        Line("AUTOAMKB – kørselsinfo");
        row++;
        Line("Projekt", report.ProjectId);
        Line("Etape", report.EtapeId);
        Line("Tegning", report.DrawingPath);
        Line("Kørt", report.RunTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        Line("Bruger", Environment.UserName);
        Line("Konfiguration", report.Configuration);
        Line("Regler", report.Rules.Source);

        row++;
        Line("Ikke vurderet");
        foreach (string item in report.NotEvaluated) Line("", item);

        row++;
        Line("Antal");
        foreach (HitKind kind in HitKind.All)
        {
            Line(kind.Name, report.Hits.Count(hit => hit.Kind == kind).ToString(CultureInfo.InvariantCulture));
        }
        Line("Ventilplaceringer", report.Valves.Count.ToString(CultureInfo.InvariantCulture));

        row++;
        Line("Datafiler");
        foreach (DataFileInfo file in report.DataFiles)
        {
            Line(file.Role, file.Path, file.Modified.Match(time => time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), () => ""));
        }

        row++;
        Line("Advarsler");
        if (report.Warnings.Count == 0) Line("", "Ingen");
        foreach (string warning in report.Warnings.Distinct()) Line("", warning);

        row++;
        Line("Regler i brug", "Værdi", "Note");
        foreach (RuleEntry entry in report.Rules.Rules.Entries) Line(entry.Key, entry.Value, entry.Note);

        return new XlsxWorksheet(
            "Kørselsinfo",
            cells,
            new[] { new XlsxColumnWidth(1, 30), new XlsxColumnWidth(2, 110), new XlsxColumnWidth(3, 60) });
    }

    private static string CopyHint(int rowCount) =>
        rowCount == 0
            ? "Ingen fund."
            : $"Kopiér B{FirstDataRow}:F{FirstDataRow + rowCount - 1} og indsæt som værdier i skabelonens celle B{FirstDataRow}. Kolonne Q og frem er kildedata.";

    private static List<XlsxCell> Intro(string title, string hint, AmkReport report) => new List<XlsxCell>
    {
        new XlsxCell(1, 2, XlsxValue.Of(title)),
        new XlsxCell(2, 2, XlsxValue.Of(hint)),
        new XlsxCell(3, 2, XlsxValue.Of(
            $"Projekt {report.ProjectId}, etape {report.EtapeId}, kørt {report.RunTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}. Se fanen Kørselsinfo.")),
    };

    private static IEnumerable<XlsxCell> Headers(IReadOnlyList<string> headers) =>
        headers.Select((header, index) => new XlsxCell(HeaderRow, 2 + index, XlsxValue.Of(header)));

    private static IEnumerable<XlsxCell> SourceHeaders(IReadOnlyList<string> headers) =>
        headers.Select((header, index) => new XlsxCell(HeaderRow, SourceColumn + index, XlsxValue.Of(header)));

    private static IEnumerable<XlsxCell> Row(int row, params string[] valuesFromB) =>
        valuesFromB.Select((value, index) => new XlsxCell(row, 2 + index, XlsxValue.Of(value)));

    private static IEnumerable<XlsxCell> SourceRow(int row, params XlsxValue[] values) =>
        values.Select((value, index) => new XlsxCell(row, SourceColumn + index, value));
}
