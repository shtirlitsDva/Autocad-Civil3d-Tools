using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System.Linq;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §6 Forsyningspunkter: supply point table with kote, pressure, temperatures.
/// </summary>
internal class SupplyPointsModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.SupplyPoints;
    public string DisplayName => "Forsyningspunkter";
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

            page.Header().Text("6  Forsyningspunkter")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(ReportStyles.SectionSpacing);

                col.Item().Element(c => ComposeSupplyPointTable(c, context));

                bool anyNullKote = context.SupplyPoints.Any(sp => sp.KoteM == null);
                if (anyNullKote)
                {
                    col.Item().Text("Kote kr\u00e6ver konfigureret terr\u00e6ndata (GDAL).")
                        .FontSize(ReportStyles.FontSizeSmall).Italic();
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

    private static void ComposeSupplyPointTable(IContainer container, ReportDataContext ctx)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1);   // ID
                columns.RelativeColumn(1.5f); // Type
                columns.RelativeColumn(1);   // Kote [m]
                columns.RelativeColumn(1.5f); // Differenstryk [bar]
                columns.RelativeColumn(1.2f); // TFremloeb [C]
                columns.RelativeColumn(1.2f); // TRetur [C]
                columns.RelativeColumn(1.2f); // Kapacitet [MW]
            });

            table.Header(header =>
            {
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("ID").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Type").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Kote [m]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Differenstryk [bar]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("TFreml\u00f8b [\u00b0C]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("TRetur [\u00b0C]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                header.Cell().Background(ReportStyles.ColorHeaderBg)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text("Kapacitet [MW]").FontSize(ReportStyles.FontSizeSmall).SemiBold();
            });

            foreach (var sp in ctx.SupplyPoints)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text($"{sp.NodeId}").FontSize(ReportStyles.FontSizeSmall);
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(sp.Type ?? "-").FontSize(ReportStyles.FontSizeSmall);
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text(sp.KoteM.HasValue ? $"{sp.KoteM.Value:F2}" : "N/A")
                    .FontSize(ReportStyles.FontSizeSmall);
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text($"{sp.DifferentialPressureBar:F2}").FontSize(ReportStyles.FontSizeSmall);
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text($"{sp.TForwardC:F1}").FontSize(ReportStyles.FontSizeSmall);
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text($"{sp.TReturnC:F1}").FontSize(ReportStyles.FontSizeSmall);
                table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                    .Padding(ReportStyles.TableCellPadding)
                    .Text($"{sp.CapacityMw:F3}").FontSize(ReportStyles.FontSizeSmall);
            }
        });
    }
}
