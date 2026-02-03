namespace NorsynHydraulicTester.Models;

public class LookupTableRow
{
    public string PipeType { get; set; } = string.Empty;
    public int DN { get; set; }
    public double InnerDiameter { get; set; }
    public double MaxVelocity { get; set; }
    public int MaxPressureGradient { get; set; }
    public double? MaxFlowSupply { get; set; }
    public double? MaxFlowReturn { get; set; }
}
