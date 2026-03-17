using DimensioneringV2.Models.Report;

using QuestPDF.Infrastructure;

namespace DimensioneringV2.Services.Report;

internal interface IReportModule
{
    ReportModuleId Id { get; }
    string DisplayName { get; }
    bool IsImplemented { get; }
    void Compose(IDocumentContainer container, ReportDataContext context);
}
