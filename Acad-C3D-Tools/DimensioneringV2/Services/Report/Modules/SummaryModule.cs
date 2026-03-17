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
        container.Page(page =>
        {
            page.Size(ReportStyles.PageSizeA4);
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(ReportStyles.MarginTop, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Header().Text("3  Sammenfatning")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(ReportStyles.SectionSpacing);

                // §3.1 Teknologivalg
                col.Item().Text("3.1  Teknologivalg")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeTechnologyTable(c, context));

                // §3.2 Dimensionerende driftsforhold
                col.Item().Text("3.2  Dimensionerende driftsforhold")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeOperatingConditionsTable(c, context));

                // §3.3 Hovedresultater
                col.Item().Text("3.3  Hovedresultater")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();
                col.Item().Element(c => ComposeResultsTable(c, context));

                // §3.4 Overholdelse af krav
                col.Item().Text("3.4  Overholdelse af krav")
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

        var rows = new List<(string Label, string Value)>
        {
            ("Designtemperatur [°C]", $"{s.TempFrem:F0} / {s.TempFrem - s.AfkølingVarme:F0}"),
            ("Designtryk [bar]", dp.HasValue ? $"{dp.Value:F1}" : "-"),
            ("Rørtyper (fordelingsledninger)", flPipeTypes),
            ("Rørtyper (stikledninger)", slPipeTypes),
        };

        RenderKeyValueTable(container, rows);
    }

    private static void ComposeOperatingConditionsTable(IContainer container, ReportDataContext ctx)
    {
        var s = ctx.Settings;

        var rows = new List<(string Label, string Value)>
        {
            ("Fremløbstemperatur [°C]", $"{s.TempFrem:F0}"),
            ("Afkøling varme ΔT [K]", $"{s.AfkølingVarme:F0}"),
            ("Afkøling brugsvand ΔT [K]", $"{s.AfkølingBrugsvand:F0}"),
            ("Maks. tilladt tryktab i stikledning [bar]", $"{s.MaxPressureLossStikSL:F2}"),
            ("Min. differenstryk over hovedhaner [bar]",
                $"{s.MinDifferentialPressureOverHovedHaner:F2}"),
        };

        RenderKeyValueTable(container, rows);
    }

    private static void ComposeResultsTable(IContainer container, ReportDataContext ctx)
    {
        var sum = ctx.Summary;

        var rows = new List<(string Label, string Value)>
        {
            ("Samlet antal forbrugere [stk]", $"{sum.TotalBuildings:N0}"),
            ("Samlet antal boliger [stk]", $"{sum.TotalUnits:N0}"),
            ("Samlet varmebehov [MWh]", $"{sum.TotalHeatingDemandMwh:N1}"),
            ("Samlet effektbehov [MW]", $"{sum.TotalPowerDemandMw:N3}"),
            ("Samlet volumen flow [m³/h]", $"{sum.TotalFlowM3H:N2}"),
            ("Samlet tryktab til kritisk forbruger [bar]",
                $"{sum.CriticalPathPressureLossBar:N3}"),
            ("Samlet distributionslednings længde [m]",
                $"{sum.DistributionLineLengthM:N1}"),
            ("Samlet stiklednings længde [m]", $"{sum.ServiceLineLengthM:N1}"),
            ("Samlet pris [kr]", $"{sum.TotalPriceDkk:N0}"),
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
        IContainer container, List<(string Label, string Value)> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
            });

            foreach (var (label, value) in rows)
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
            }
        });
    }
}
