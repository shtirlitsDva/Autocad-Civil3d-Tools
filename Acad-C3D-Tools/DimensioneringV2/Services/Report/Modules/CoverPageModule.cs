using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders the cover page (§1) and version history (§2).
/// </summary>
internal class CoverPageModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.CoverPage;
    public string DisplayName => "Forside";
    public bool IsImplemented => true;
    public bool HasSectionNumber => false;

    public void Compose(IDocumentContainer container, ReportDataContext context)
    {
        var hn = context.HnSettings;
        var network = context.Network;

        container.Page(page =>
        {
            page.Size(ReportStyles.PageSizeA4);
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(60, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Content().Column(col =>
            {
                col.Spacing(8);

                // Title area
                col.Item().PaddingTop(80).Text("Dimensioneringsrapport")
                    .FontSize(ReportStyles.FontSizeTitle)
                    .FontColor(ReportStyles.ColorPrimary)
                    .Bold();

                col.Item().PaddingTop(4).Text("Fjernvarmeledningsnet")
                    .FontSize(ReportStyles.FontSizeH1)
                    .FontColor(ReportStyles.ColorSecondary);

                col.Item().PaddingTop(30).LineHorizontal(1)
                    .LineColor(ReportStyles.ColorPrimary);

                // Metadata table
                col.Item().PaddingTop(20).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                    });

                    AddMetaRow(table, "Projektnavn", hn.ProjectName);
                    AddMetaRow(table, "Projekt nr.", hn.ProjectNumber);
                    AddMetaRow(table, "Dokument nr.", hn.DocumentNumber);
                    AddMetaRow(table, "Beregning", network.Id);
                    //AddMetaRow(table, "Beskrivelse", network.Description);
                    AddMetaRow(table, "Software", "DimensioneringV2");
                    AddMetaRow(table, "Oprettelsesdato",
                        network.CalculatedAt?.ToString("dd-MM-yyyy HH:mm") ?? "-");
                    AddMetaRow(table, "Beregningstid",
                        network.CalculationDuration?.ToString(@"hh\:mm\:ss") ?? "-");

                    table.Cell().Element(CellStyle)
                        .Text("").FontSize(ReportStyles.FontSizeBody); // spacer

                    table.Cell().Element(CellStyle)
                        .Text("").FontSize(ReportStyles.FontSizeBody);

                    AddMetaRow(table, "Udarbejdet af", hn.Author);
                    AddMetaRow(table, "Kontrolleret af", hn.Reviewer);
                    AddMetaRow(table, "Godkendt af", hn.Approver);
                });

                if (!string.IsNullOrWhiteSpace(hn.CoverNote))
                {
                    col.Item().PaddingTop(20).Text(hn.CoverNote)
                        .FontSize(ReportStyles.FontSizeBody)
                        .Italic();
                }
            });
        });

        // Version history page (§2) — only if entries exist
        if (hn.VersionHistory.Count > 0)
        {
            container.Page(page =>
            {
                page.Size(ReportStyles.PageSizeA4);
                page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
                page.MarginRight(ReportStyles.MarginRight, Unit.Point);
                page.MarginTop(ReportStyles.MarginTop, Unit.Point);
                page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

                page.Content().Column(col =>
                {
                    col.Item().Text("Versionshistorik")
                        .FontSize(ReportStyles.FontSizeH1)
                        .FontColor(ReportStyles.ColorPrimary)
                        .Bold();

                    col.Item().PaddingTop(ReportStyles.SectionSpacing).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        // Header — inline cells; the callback parameter is NOT TableDescriptor
                        table.Header(header =>
                        {
                            header.Cell().Background(ReportStyles.ColorHeaderBg)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text("Rev").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                            header.Cell().Background(ReportStyles.ColorHeaderBg)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text("Dato").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                            header.Cell().Background(ReportStyles.ColorHeaderBg)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text("Beskrivelse").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                            header.Cell().Background(ReportStyles.ColorHeaderBg)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text("Udført").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                            header.Cell().Background(ReportStyles.ColorHeaderBg)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text("Kontrolleret").FontSize(ReportStyles.FontSizeSmall).SemiBold();
                        });

                        foreach (var entry in hn.VersionHistory)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text(entry.Revision ?? "-").FontSize(ReportStyles.FontSizeSmall);
                            table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text(entry.Date ?? "-").FontSize(ReportStyles.FontSizeSmall);
                            table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text(entry.Description ?? "-").FontSize(ReportStyles.FontSizeSmall);
                            table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text(entry.PerformedBy ?? "-").FontSize(ReportStyles.FontSizeSmall);
                            table.Cell().BorderBottom(0.5f).BorderColor(ReportStyles.ColorBorderLight)
                                .Padding(ReportStyles.TableCellPadding)
                                .Text(entry.ReviewedBy ?? "-").FontSize(ReportStyles.FontSizeSmall);
                        }
                    });
                });
            });
        }
    }

    private static void AddMetaRow(TableDescriptor table, string label, string? value)
    {
        table.Cell().Element(CellStyle)
            .Text(label).FontSize(ReportStyles.FontSizeBody).SemiBold();
        table.Cell().Element(CellStyle)
            .Text(value ?? "-").FontSize(ReportStyles.FontSizeBody);
    }

    private static IContainer CellStyle(IContainer container) =>
        container.PaddingVertical(3).PaddingHorizontal(4);
}
