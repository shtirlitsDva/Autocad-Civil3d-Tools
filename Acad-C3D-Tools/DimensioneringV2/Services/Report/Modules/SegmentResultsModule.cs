using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.DataModels;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §7.4 Strækningsresultater: a paginated table of all pipe segments
/// with dimensions, velocities, and pressure losses.
/// </summary>
internal class SegmentResultsModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.SegmentResults;
    public string DisplayName => "Strækningsresultater";
    public bool IsImplemented => true;

    public void Compose(IDocumentContainer container, ReportDataContext context)
    {
        container.Page(page =>
        {
            // Use landscape for this wide table
            page.Size(PageSizes.A4.Landscape());
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(ReportStyles.MarginTop, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Header().Text("7.4  Strækningsresultater")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);    // Stræknings-id
                    columns.RelativeColumn(1);    // L [m]
                    columns.RelativeColumn(1.5f); // Rørtype
                    columns.RelativeColumn(1.2f); // Dimension
                    columns.RelativeColumn(1);    // Hastighed [m/s]
                    columns.RelativeColumn(1);    // Udnyttelse [%]
                    columns.RelativeColumn(1.2f); // Trykgradient [Pa/m]
                    columns.RelativeColumn(1);    // Tryktab [bar]
                });

                table.Header(header =>
                {
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text("Stræknings-id").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("L [m]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text("Rørtype").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text("Dimension").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Hastighed [m/s]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Udnyttelse [%]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Trykgradient [Pa/m]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                    header.Cell().Background(ReportStyles.ColorHeaderBg)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text("Tryktab [bar]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                });

                bool alternate = false;
                foreach (var seg in context.Segments)
                {
                    var bg = alternate ? ReportStyles.ColorAlternateRowBg : "#FFFFFF";

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text(seg.SegmentId).FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{seg.LengthM:F1}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text(seg.PipeType).FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .Text(seg.DimensionName).FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{seg.VelocitySupply:F2}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{seg.VelocityUtilization:F1}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{seg.PressureGradientSupply:F1}").FontSize(ReportStyles.FontSizeSmall);

                    table.Cell().Background(bg)
                        .BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                        .Padding(ReportStyles.TableCellPadding)
                        .AlignRight()
                        .Text($"{seg.PressureLossBar:F3}").FontSize(ReportStyles.FontSizeSmall);

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
