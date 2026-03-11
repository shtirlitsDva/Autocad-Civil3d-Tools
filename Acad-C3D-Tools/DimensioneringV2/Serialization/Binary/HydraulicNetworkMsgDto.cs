using DimensioneringV2.Models;

using MessagePack;

using NorsynHydraulicCalc;

using System;
using System.Linq;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal class HydraulicNetworkMsgDto
{
    [Key(0)] public string? Id { get; set; }
    [Key(1)] public DateTime? CalculatedAt { get; set; }
    [Key(2)] public long? CalculationDurationTicks { get; set; }
    [Key(3)] public double TotalPrice { get; set; }
    [Key(4)] public HydraulicSettingsMsgDto? FrozenSettings { get; set; }
    [Key(5)] public UndirectedGraphMsgDto[] Graphs { get; set; } = Array.Empty<UndirectedGraphMsgDto>();

    public static HydraulicNetworkMsgDto FromDomain(HydraulicNetwork hn)
    {
        return new HydraulicNetworkMsgDto
        {
            Id = hn.Id,
            CalculatedAt = hn.CalculatedAt,
            CalculationDurationTicks = hn.CalculationDuration?.Ticks,
            TotalPrice = hn.TotalPrice,
            FrozenSettings = hn.FrozenSettings != null
                ? HydraulicSettingsMsgDto.FromDomain(hn.FrozenSettings)
                : null,
            Graphs = hn.Graphs
                .Select(UndirectedGraphMsgDto.FromDomain)
                .ToArray(),
        };
    }

    public HydraulicNetwork ToDomain()
    {
        var graphs = Graphs
            .Select(g => g.ToDomain())
            .ToList();

        var frozenSettings = FrozenSettings?.ToDomain();

        return HydraulicNetwork.Restore(
            Id,
            graphs,
            frozenSettings,
            CalculatedAt,
            CalculationDurationTicks.HasValue
                ? TimeSpan.FromTicks(CalculationDurationTicks.Value)
                : null,
            TotalPrice);
    }
}
