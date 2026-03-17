namespace DimensioneringV2.Services.Report.DataModels;

/// <summary>
/// Flat data row for a consumer (building) in the report (§8).
/// </summary>
internal record ConsumerRow(
    string Address,
    string BuildingType,
    string BuildingCode,
    int NumberOfProperties,
    int NumberOfUnitsWithHotWater,
    double DimCoolingC,
    double BbrAreaM2,
    int ConstructionYear,
    double EnergyConsumptionKwhYear,
    double ServiceLineLengthM,
    string DimensionName,
    double PressureGradientPaM,
    double VelocityMs,
    double PressureLossServiceLineBar,
    double RequiredDifferentialPressureBar);
