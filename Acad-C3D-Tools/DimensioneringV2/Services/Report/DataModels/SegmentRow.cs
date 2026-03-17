namespace DimensioneringV2.Services.Report.DataModels;

/// <summary>
/// Flat data row for a pipe segment in the report (§7.4).
/// </summary>
internal record SegmentRow(
    string SegmentId,
    double LengthM,
    string PipeType,
    string DimensionName,
    double VelocitySupply,
    double VelocityReturn,
    double VelocityUtilization,
    double PressureGradientSupply,
    double PressureGradientReturn,
    double PressureGradientUtilization,
    double PressureLossBar);
