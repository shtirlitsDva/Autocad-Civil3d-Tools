namespace DimensioneringV2.Services.Report.DataModels;

/// <summary>
/// A single compliance check row for the report (§3.4, §7.2).
/// </summary>
internal record ComplianceRow(
    string CheckName,
    string Criterion,
    string CalculatedValue,
    bool Passes);
