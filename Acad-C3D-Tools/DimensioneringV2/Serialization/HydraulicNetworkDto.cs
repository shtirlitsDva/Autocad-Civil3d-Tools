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
    public string? Description { get; set; }
    public BbrFeatureDto[]? BbrFeatures { get; set; }

    public HydraulicNetworkDto() { }

    public HydraulicNetworkDto(HydraulicNetwork hn)
    {
        Id = hn.Id;
        Description = hn.Description;
        CalculatedAt = hn.CalculatedAt;
        CalculationDurationTicks = hn.CalculationDuration?.Ticks;
        TotalPrice = hn.TotalPrice;
        FrozenSettings = hn.FrozenSettings;
        Graphs = hn.Graphs.ToArray();
        BbrFeatures = hn.BbrFeatures?.Select(f => new BbrFeatureDto
        {
            HeatingType = f.HeatingType,
            Address = f.Address,
            OriginalX = f.OriginalX,
            OriginalY = f.OriginalY,
        }).ToArray();
    }

    public HydraulicNetwork ToHydraulicNetwork()
    {
        var bbrFeatures = BbrFeatures?.Select(b =>
            new BBRMapFeature(
                new NetTopologySuite.Geometries.Point(b.OriginalX, b.OriginalY),
                b.HeatingType, b.Address, b.OriginalX, b.OriginalY))
            .ToList();

        return HydraulicNetwork.Restore(
            Id,
            Graphs.ToList(),
            FrozenSettings,
            CalculatedAt,
            CalculationDurationTicks.HasValue
                ? TimeSpan.FromTicks(CalculationDurationTicks.Value)
                : null,
            TotalPrice,
            Description,
            bbrFeatures);
    }
}

internal class BbrFeatureDto
{
    public string HeatingType { get; set; } = "";
    public string Address { get; set; } = "";
    public double OriginalX { get; set; }
    public double OriginalY { get; set; }
}
