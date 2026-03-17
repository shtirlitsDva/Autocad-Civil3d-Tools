using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.DataModels;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System.Collections.Generic;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §3 Sammenfatning: technology choice, operating conditions,
/// key results, and compliance checks.
/// </summary>
internal class SummaryModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.Summary;
    public string DisplayName => "Sammenfatning";
    public bool IsImplemented => true;

    public void Compose(IDocumentContainer container, ReportDataContext context)
    {
        var s = context.CurrentSection;

        container.Page(page =>
        {
            page.Size(ReportStyles.PageSizeA4);
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(ReportStyles.MarginTop, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Header().Text($"{s}  Sammenfatning")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(ReportStyles.SectionSpacing);

                // §s.1 Teknologivalg
                col.Item().Text($"{s}.1  Teknologivalg")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeTechnologyTable(c, context));

                // §s.2 Dimensionerende driftsforhold
                col.Item().Text($"{s}.2  Dimensionerende driftsforhold")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeOperatingConditionsTable(c, context));

                // §s.3 Hovedresultater
                col.Item().Text($"{s}.3  Hovedresultater")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeResultsTable(c, context));

                // §s.4 Overholdelse af krav
                col.Item().Text($"{s}.4  Overholdelse af krav")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeComplianceTable(c, context));
            });

            page.Footer().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(ReportStyles.FontSizeFooter));
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    private static void ComposeTechnologyTable(IContainer container, ReportDataContext ctx)
    {
        var s = ctx.Settings;
        var dp = ctx.HnSettings.DesignPressureBar;

        // Collect distinct pipe types used in FL and SL
        string flPipeTypes = GetDistinctPipeTypes(ctx, NorsynHydraulicCalc.SegmentType.Fordelingsledning);
        string slPipeTypes = GetDistinctPipeTypes(ctx, NorsynHydraulicCalc.SegmentType.Stikledning);

        var rows = new List<(string Label, string Value, string Unit)>
        {
            ("Designtemperatur", $"{s.TempFrem:F0} / {s.TempFrem - s.AfkølingVarme:F0}", "°C"),
            ("Designtryk", dp.HasValue ? $"{dp.Value:F1}" : "-", "bar"),
            ("Rørtyper (fordelingsledninger)", flPipeTypes, ""),
            ("Rørtyper (stikledninger)", slPipeTypes, ""),
        };

        RenderKeyValueTable(container, rows);
    }

    private static void ComposeOperatingConditionsTable(IContainer container, ReportDataContext ctx)
    {
        var s = ctx.Settings;

        var rows = new List<(string Label, string Value, string Unit)>
        {
            ("Fremløbstemperatur", $"{s.TempFrem:F0}", "°C"),
            ("Afkøling varme ΔT", $"{s.AfkølingVarme:F0}", "K"),
            ("Afkøling brugsvand ΔT", $"{s.AfkølingBrugsvand:F0}", "K"),
            ("Maks. tilladt tryktab i stikledning", $"{s.MaxPressureLossStikSL:F2}", "bar"),
            ("Min. differenstryk over hovedhaner",
                $"{s.MinDifferentialPressureOverHovedHaner:F2}", "bar"),
        };

        RenderKeyValueTable(container, rows);
    }

    private static void ComposeResultsTable(IContainer container, ReportDataContext ctx)
    {
        var sum = ctx.Summary;

        var rows = new List<(string Label, string Value, string Unit)>
        {
            ("Samlet antal bygninger", $"{sum.TotalBuildings:N0}", "stk"),
            ("Samlet antal enheder", $"{sum.TotalUnits:N0}", "stk"),
            ("Samlet varmebehov", $"{sum.TotalHeatingDemandMwh:N1}", "MWh"),
            ("Samlet effektbehov", $"{sum.TotalPowerDemandMw:N3}", "MW"),
            ("Samlet volumen flow", $"{sum.TotalFlowM3H:N2}", "m³/h"),
            ("Samlet tryktab til kritisk forbruger",
                $"{sum.CriticalPathPressureLossBar:N3}", "bar"),
            ("Samlet distributionslednings længde",
                $"{sum.DistributionLineLengthM:N1}", "m"),
            ("Samlet stiklednings længde", $"{sum.ServiceLineLengthM:N1}", "m"),
            ("Samlet pris", $"{sum.TotalPriceDkk:N0}", "kr"),
        };

        RenderKeyValueTable(container, rows);
    }

    private static void ComposeComplianceTable(IContainer container, ReportDataContext ctx)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
            });

            table.Header(header =>
            {
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Kontrolpunkt").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Designkriterie").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Beregnet værdi").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Status").FontSize(ReportStyles.FontSizeSmall).SemiBold();
            });

            foreach (var row in ctx.ComplianceChecks)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(row.CheckName ?? "-").FontSize(ReportStyles.FontSizeSmall);
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(row.Criterion ?? "-").FontSize(ReportStyles.FontSizeSmall);
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(row.CalculatedValue ?? "-").FontSize(ReportStyles.FontSizeSmall);

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(row.Passes ? "OK" : "Ikke OK")
                    .FontSize(ReportStyles.FontSizeSmall)
                    .FontColor(row.Passes ? ReportStyles.ColorPass : ReportStyles.ColorFail)
                    .Bold();
            }
        });
    }

    private static string GetDistinctPipeTypes(ReportDataContext ctx, NorsynHydraulicCalc.SegmentType segType)
    {
        var types = new HashSet<string>();
        foreach (var graph in ctx.Network.Graphs)
        {
            foreach (var edge in graph.Edges)
            {
                var f = edge.PipeSegment;
                if (f.NumberOfBuildingsSupplied == 0) continue;
                if (f.SegmentType == segType)
                    types.Add(f.Dim.PipeType.ToString());
            }
        }
        return types.Count > 0 ? string.Join(", ", types) : "-";
    }

    private static void RenderKeyValueTable(
        IContainer container, List<(string Label, string Value, string Unit)> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.RelativeColumn(2);
                columns.ConstantColumn(50);
            });

            foreach (var (label, value, unit) in rows)
            {
                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(label).FontSize(ReportStyles.FontSizeBody);

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text(value).FontSize(ReportStyles.FontSizeBody);

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .PaddingLeft(6).PaddingVertical(ReportStyles.TableCellPadding)
                    .Text(unit).FontSize(ReportStyles.FontSizeBody);
            }
        });
    }
}
