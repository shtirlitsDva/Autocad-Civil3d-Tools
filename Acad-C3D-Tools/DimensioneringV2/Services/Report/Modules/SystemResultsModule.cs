using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System.Collections.Generic;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §7.1-7.2 Systemresultater: system overview and compliance checks.
/// In multi-network mode, also renders per-network sub-sections.
/// </summary>
internal class SystemResultsModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.SystemResults;
    public string DisplayName => "Systemresultater";
    public bool IsImplemented => true;
    public NetworkAffinity Affinity => NetworkAffinity.Both;

    public void Compose(IDocumentContainer container, ReportDataContext context)
    {
        var s = context.CurrentSection;
        var scope = context.Scope!;

        if (scope.IsTotal)
        {
            ComposeTotal(container, context, s, scope);
        }
        else
        {
            ComposePerNetwork(container, context, s, scope);
        }
    }

    private static void ComposeTotal(
        IDocumentContainer container, ReportDataContext context, int s, NetworkScope scope)
    {
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
                col.Item().Element(c => ComposeSystemOverviewTable(c, context,
                    skipCriticalPath: !scope.IsSingleNetworkMode));

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

    private static void ComposePerNetwork(
        IDocumentContainer container, ReportDataContext context, int s, NetworkScope scope)
    {
        var sub = ++context.SubSectionCounter;

        container.Page(page =>
        {
            page.Size(ReportStyles.PageSizeA4);
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(ReportStyles.MarginTop, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Header().Text($"{s}.{sub}  {scope.NetworkDisplayName}")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(ReportStyles.SectionSpacing);

                // Systemoversigt (includes CriticalPathPressureLossBar for per-network)
                col.Item().Text("Systemoversigt")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeSystemOverviewTable(c, context, skipCriticalPath: false));

                // Dimensioneringskontrol
                col.Item().Text("Dimensioneringskontrol")
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

    private static void ComposeSystemOverviewTable(
        IContainer container, ReportDataContext ctx, bool skipCriticalPath)
    {
        var sum = ctx.Summary;

        var rows = new List<(string Label, string Value, string Unit)>
        {
            ("Samlet antal bygninger", $"{sum.TotalBuildings:N0}", "stk"),
            ("Samlet antal enheder", $"{sum.TotalUnits:N0}", "stk"),
            ("Samlet varmebehov", $"{sum.TotalHeatingDemandMwh:N1}", "MWh"),
            ("Samlet effektbehov (Φ = qᵥ·ρ·cₚ·ΔT)", $"{sum.TotalPowerDemandKw:N1}", "kW"),
            ("Samlet volumen flow", $"{sum.TotalFlowM3H:N2}", "m\u00b3/h"),
        };

        if (!skipCriticalPath)
        {
            rows.Add(("Samlet tryktab til kritisk forbruger",
                $"{sum.CriticalPathPressureLossBar:N3}", "bar"));
        }

        rows.Add(("Samlet distributionslednings l\u00e6ngde",
            $"{sum.DistributionLineLengthM:N1}", "m"));
        rows.Add(("Samlet stiklednings l\u00e6ngde", $"{sum.ServiceLineLengthM:N1}", "m"));
        rows.Add(("Samlet pris", $"{sum.TotalPriceDkk:N0}", "kr"));

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

        container.PaddingTop(4).Text(ReportStyles.PowerNote)
            .FontSize(ReportStyles.FontSizeSmall).Italic();
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
