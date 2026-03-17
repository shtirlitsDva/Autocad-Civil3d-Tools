using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Stub for §7.3 Følsomhedsanalyse — not yet implemented.
/// </summary>
internal class SensitivityModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.Sensitivity;
    public string DisplayName => "F\u00f8lsomhedsanalyse";
    public bool IsImplemented => false;

    public void Compose(IDocumentContainer container, ReportDataContext context)
    {
        container.Page(page =>
        {
            page.Size(ReportStyles.PageSizeA4);
            page.MarginLeft(ReportStyles.MarginLeft, Unit.Point);
            page.MarginRight(ReportStyles.MarginRight, Unit.Point);
            page.MarginTop(ReportStyles.MarginTop, Unit.Point);
            page.MarginBottom(ReportStyles.MarginBottom, Unit.Point);

            page.Header().Text($"{context.CurrentSection}  Følsomhedsanalyse")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Item().Text($"Sektion {context.CurrentSection} Følsomhedsanalyse — Ikke inkluderet i denne version.")
                    .FontSize(ReportStyles.FontSizeBody).Italic();
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
