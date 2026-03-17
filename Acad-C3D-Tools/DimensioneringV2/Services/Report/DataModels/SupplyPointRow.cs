namespace DimensioneringV2.Services.Report.DataModels;

/// <summary>
/// Data row for a supply point in the report (§6).
/// </summary>
internal record SupplyPointRow(
    int NodeId,
    string Type,
    double? KoteM,
    double DifferentialPressureBar,
    double TForwardC,
    double TReturnC,
    double CapacityMw);
