using DimensioneringV2.Services.Report.DataModels;
using System.Collections.Generic;

namespace DimensioneringV2.Services.Report;

internal record ScopedReportData(
    SystemSummary Summary,
    List<ComplianceRow> ComplianceChecks,
    List<SupplyPointRow> SupplyPoints);
