using DimensioneringV2.Models.Report;

using QuestPDF.Infrastructure;

namespace DimensioneringV2.Services.Report;

internal interface IReportModule
{
    ReportModuleId Id { get; }
    string DisplayName { get; }
    bool IsImplemented { get; }

    /// <summary>
    /// Whether this module gets a numbered section heading.
    /// False for modules like the cover page that don't have section numbers.
    /// </summary>
    bool HasSectionNumber => true;

    void Compose(IDocumentContainer container, ReportDataContext context);
}
