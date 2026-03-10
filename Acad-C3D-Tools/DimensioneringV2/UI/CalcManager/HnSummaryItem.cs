using DimensioneringV2.Models;

using System;

namespace DimensioneringV2.UI.CalcManager;

internal class HnSummaryItem
{
    public string Id { get; }
    public DateTime? CalculatedAt { get; }
    public string Duration { get; }
    public double TotalPrice { get; }
    public bool IsSaved { get; set; }
    public string Status => IsSaved ? "Gemt" : "* I hukommelse";

    public HydraulicNetwork Hn { get; }

    public HnSummaryItem(HydraulicNetwork hn)
    {
        Hn = hn;
        Id = hn.Id ?? "(unnamed)";
        CalculatedAt = hn.CalculatedAt;
        Duration = hn.CalculationDuration.HasValue
            ? hn.CalculationDuration.Value.ToString(@"hh\:mm\:ss")
            : "-";
        TotalPrice = hn.TotalPrice;
        IsSaved = hn.IsSaved;
    }
}
