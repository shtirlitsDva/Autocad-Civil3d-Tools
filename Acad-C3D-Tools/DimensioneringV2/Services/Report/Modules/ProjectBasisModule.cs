using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.Styles;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DimensioneringV2.Services.Report.Modules;

/// <summary>
/// Renders §4 Projektgrundlag: norm/regulation basis and deviations.
/// </summary>
internal class ProjectBasisModule : IReportModule
{
    public ReportModuleId Id => ReportModuleId.ProjectBasis;
    public string DisplayName => "Projektgrundlag";
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

            page.Header().Text($"{s}  Projektgrundlag")
                .FontSize(ReportStyles.FontSizeH1)
                .FontColor(ReportStyles.ColorPrimary).Bold();

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(ReportStyles.SectionSpacing);

                // §s.1 Norm- og regelgrundlag
                col.Item().Text($"{s}.1  Norm- og regelgrundlag")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();

                string normText = string.IsNullOrWhiteSpace(context.Profile.NormText)
                    ? "Ingen norm- eller regelgrundlag angivet."
                    : context.Profile.NormText;

                col.Item().Text(normText).FontSize(ReportStyles.FontSizeBody);

                // §s.2 Afvigelser / supplerende krav
                col.Item().Text($"{s}.2  Afvigelser / supplerende krav")
                    .FontSize(ReportStyles.FontSizeH2).SemiBold();

                string deviationsText = string.IsNullOrWhiteSpace(context.Profile.DeviationsText)
                    ? "Ingen afvigelser angivet."
                    : context.Profile.DeviationsText;

                col.Item().Text(deviationsText).FontSize(ReportStyles.FontSizeBody);
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
