using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models;

using NorsynHydraulicCalc;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Serialization;

internal class HydraulicNetworkDto
{
    public string? Id { get; set; }
    public DateTime? CalculatedAt { get; set; }
    public long? CalculationDurationTicks { get; set; }
    public double TotalPrice { get; set; }
    public HydraulicSettings? FrozenSettings { get; set; }
    public UndirectedGraph<NodeJunction, EdgePipeSegment>[] Graphs { get; set; }

    public HydraulicNetworkDto() { }

    public HydraulicNetworkDto(HydraulicNetwork hn)
    {
        Id = hn.Id;
        CalculatedAt = hn.CalculatedAt;
        CalculationDurationTicks = hn.CalculationDuration?.Ticks;
        TotalPrice = hn.TotalPrice;
        FrozenSettings = hn.FrozenSettings;
        Graphs = hn.Graphs.ToArray();
    }

    public HydraulicNetwork ToHydraulicNetwork()
    {
        return HydraulicNetwork.Restore(
            Id,
            Graphs.ToList(),
            FrozenSettings,
            CalculatedAt,
            CalculationDurationTicks.HasValue
                ? TimeSpan.FromTicks(CalculationDurationTicks.Value)
                : null,
            TotalPrice);
    }
}
