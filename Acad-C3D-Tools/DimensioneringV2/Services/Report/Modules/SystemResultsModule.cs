using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System.Collections.Generic;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §7.1-7.2 Systemresultater: system overview and compliance checks.
/// </summary>
internal class SystemResultsModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.SystemResults;
    public string DisplayName => "Systemresultater";
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

            page.Header().Text($"{s}  Systemresultater")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(ReportStyles.SectionSpacing);

                // §s.1 Systemoversigt
                col.Item().Text($"{s}.1  Systemoversigt")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeSystemOverviewTable(c, context));

                // §s.2 Dimensioneringskontrol
                col.Item().Text($"{s}.2  Dimensioneringskontrol")
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

    private static void ComposeSystemOverviewTable(IContainer container, ReportDataContext ctx)
    {
        var sum = ctx.Summary;

        var rows = new List<(string Label, string Value, string Unit)>
        {
            ("Samlet antal bygninger", $"{sum.TotalBuildings:N0}", "stk"),
            ("Samlet antal enheder", $"{sum.TotalUnits:N0}", "stk"),
            ("Samlet varmebehov", $"{sum.TotalHeatingDemandMwh:N1}", "MWh"),
            ("Samlet effektbehov", $"{sum.TotalPowerDemandMw:N3}", "MW"),
            ("Samlet volumen flow", $"{sum.TotalFlowM3H:N2}", "m\u00b3/h"),
            ("Samlet tryktab til kritisk forbruger",
                $"{sum.CriticalPathPressureLossBar:N3}", "bar"),
            ("Samlet distributionslednings l\u00e6ngde",
                $"{sum.DistributionLineLengthM:N1}", "m"),
            ("Samlet stiklednings l\u00e6ngde", $"{sum.ServiceLineLengthM:N1}", "m"),
            ("Samlet pris", $"{sum.TotalPriceDkk:N0}", "kr"),
        };

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
                    .Text("Beregnet v\u00e6rdi").FontSize(ReportStyles.FontSizeSmall).SemiBold();
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
}
