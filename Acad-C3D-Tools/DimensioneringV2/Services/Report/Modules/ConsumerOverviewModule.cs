using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.DataModels;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §8 Forbrugeroversigt: summary stats, top-10 critical consumers,
/// and a detailed consumer table in landscape format.
/// </summary>
internal class ConsumerOverviewModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.ConsumerOverview;
    public string DisplayName => "Forbrugeroversigt";
    public bool IsImplemented => true;

    public void Compose(IDocumentContainer container, ReportDataContext context)
    {
        // §8.1 & §8.2 — Portrait page with summary and top-10
        container.Page(page =>
        {
            page.Size(ReportStyles.PageSizeA4);
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(ReportStyles.MarginTop, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Header().Text("8  Forbrugeroversigt")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(ReportStyles.SectionSpacing);

                // §8.1 Samlet oversigt
                col.Item().Text("8.1  Samlet oversigt")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeSummaryTable(c, context));

                // §8.2 Top-10 kritiske forbrugere
                col.Item().Text("8.2  Top-10 kritiske forbrugere")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeTop10Table(c, context));
            });

            page.Footer().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(ReportStyles.FontSizeFooter));
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });

        // §8.3 Detaljeret forbrugeroversigt — Landscape page
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(ReportStyles.MarginTop, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Header().Text("8.3  Detaljeret forbrugeroversigt")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Element(c => ComposeDetailedTable(c, context));

            page.Footer().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(ReportStyles.FontSizeFooter));
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    private static void ComposeSummaryTable(IContainer container, ReportDataContext ctx)
    {
        var consumers = ctx.Consumers;
        int count = consumers.Count;
        int totalUnits = consumers.Sum(c => c.NumberOfProperties);
        double totalServiceLength = consumers.Sum(c => c.ServiceLineLengthM);
        double avgServiceLength = count > 0 ? totalServiceLength / count : 0;

        var rows = new List<(string Label, string Value, string Unit)>
        {
            ("Antal forbrugere", $"{count:N0}", "stk"),
            ("Antal boliger", $"{totalUnits:N0}", "stk"),
            ("Samlet stiklængde", $"{totalServiceLength:F1}", "m"),
            ("Gennemsnitlig stiklængde", $"{avgServiceLength:F1}", "m"),
        };

        RenderKeyValueTable(container, rows);
    }

    private static void ComposeTop10Table(IContainer container, ReportDataContext ctx)
    {
        var top10 = ctx.Consumers.Take(10).ToList();

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(30);   // #
                columns.RelativeColumn(4);    // Adresse
                columns.RelativeColumn(2);    // Nødvendigt differenstryk [bar]
                columns.RelativeColumn(1.5f); // Udnyttelsesgrad [%]
            });

            table.Header(header =>
            {
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("#").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Adresse").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("Ndv. differenstryk [bar]")
                    .FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("Udnyttelse [%]")
                    .FontSize(ReportStyles.FontSizeSmall).SemiBold();
            });

            int rank = 1;
            foreach (var c in top10)
            {
                // Utilization: ratio of service line pressure loss to max allowed
                // Use PressureGradientPaM as a proxy for utilization if no explicit field
                double utilization = ctx.Settings.MaxPressureLossStikSL > 0
                    ? (c.PressureLossServiceLineBar / ctx.Settings.MaxPressureLossStikSL) * 100
                    : 0;

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text($"{rank}").FontSize(ReportStyles.FontSizeSmall);

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(c.Address).FontSize(ReportStyles.FontSizeSmall);

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.RequiredDifferentialPressureBar:F3}")
                    .FontSize(ReportStyles.FontSizeSmall);

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{utilization:F1}")
                    .FontSize(ReportStyles.FontSizeSmall);

                rank++;
            }
        });
    }

    private static void ComposeDetailedTable(IContainer container, ReportDataContext ctx)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);    // Adresse
                columns.RelativeColumn(1.5f); // Bygningstype
                columns.RelativeColumn(0.8f); // Antal ejd.
                columns.RelativeColumn(1);    // BBR-areal [m²]
                columns.RelativeColumn(0.8f); // Opførselsår
                columns.RelativeColumn(1.2f); // Energi [kWh/år]
                columns.RelativeColumn(0.8f); // Stik L [m]
                columns.RelativeColumn(0.8f); // DN
                columns.RelativeColumn(1);    // Trykgradient [Pa/m]
                columns.RelativeColumn(0.8f); // Hastighed [m/s]
                columns.RelativeColumn(1);    // Tryktab stik [bar]
                columns.RelativeColumn(1);    // Ndv. diff.tryk [bar]
            });

            table.Header(header =>
            {
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Adresse").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Bygningstype").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("Antal ejd.").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("BBR [m\u00b2]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("Opf.år").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("Energi [kWh/år]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("Stik L [m]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("DN").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("ΔP [Pa/m]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("v [m/s]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("ΔP stik [bar]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text("Ndv. Δp [bar]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
            });

            bool alternate = false;
            foreach (var c in ctx.Consumers)
            {
                var bg = alternate ? ReportStyles.ColorAlternateRowBg : "#FFFFFF";
                var fs = ReportStyles.FontSizeSmall;

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(c.Address).FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(c.BuildingType).FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.NumberOfProperties}").FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.BbrAreaM2:F0}").FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.ConstructionYear}").FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.EnergyConsumptionKwhYear:F0}").FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.ServiceLineLengthM:F1}").FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(c.DimensionName).FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.PressureGradientPaM:F1}").FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.VelocityMs:F2}").FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.PressureLossServiceLineBar:F3}").FontSize(fs);

                table.Cell().Background(bg)
                    .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .AlignRight()
                    .Text($"{c.RequiredDifferentialPressureBar:F3}").FontSize(fs);

                alternate = !alternate;
            }
        });
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
