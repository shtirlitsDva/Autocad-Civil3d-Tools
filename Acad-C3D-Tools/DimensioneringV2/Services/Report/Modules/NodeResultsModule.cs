using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.DataModels;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System.Linq;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §7.5 Knudepunkter: table of building and root nodes
/// with power demand and pressure results.
/// </summary>
internal class NodeResultsModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.NodeResults;
    public string DisplayName => "Knudepunkter";
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

            page.Header().Text("7.5  Knudepunkter")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);   // Knude-id
                    columns.RelativeColumn(1.5f); // Effektbehov [kW]
                    columns.RelativeColumn(1.5f); // Tryktab frem [bar]
                    columns.RelativeColumn(1.5f); // Tilg. differenstryk [bar]
                });

                table.Header(header =>
                {
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text("Knude-id").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Effektbehov [kW]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Tryktab frem [bar]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Tilg. differenstryk [bar]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                });

                var filteredNodes = context.Nodes
                    .Where(n => n.IsBuilding || n.IsRoot)
                    .OrderBy(n => n.NodeId)
                    .ToList();

                bool alternate = false;
                foreach (var node in filteredNodes)
                {
                    var bg = alternate ? ReportStyles.ColorAlternateRowBg : "#FFFFFF";

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text($"{node.NodeId}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{node.EffektKw:F2}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{node.PressureLossToNodeBar:F3}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{node.AvailableDifferentialPressureBar:F3}").FontSize(ReportStyles.FontSizeSmall);

                    alternate = !alternate;
                }
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
}
