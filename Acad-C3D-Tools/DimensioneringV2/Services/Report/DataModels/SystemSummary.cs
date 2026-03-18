namespace DimensioneringV2.Services.Report.DataModels;

/// <summary>
/// Aggregated system-level summary for the report (§3.3, §7.1).
/// </summary>
internal class SystemSummary
{
    public int TotalBuildings { get; set; }
    public int TotalUnits { get; set; }
    public double TotalHeatingDemandMwh { get; set; }
    public double TotalPowerDemandKw { get; set; }
    public double TotalFlowM3H { get; set; }
    public double DistributionLineLengthM { get; set; }
    public double ServiceLineLengthM { get; set; }
    public double TotalPriceDkk { get; set; }
    public double CriticalPathPressureLossBar { get; set; }
    public string? CriticalConsumerAddress { get; set; }
}
