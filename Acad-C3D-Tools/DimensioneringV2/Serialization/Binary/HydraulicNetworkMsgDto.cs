using DimensioneringV2.Models;

using MessagePack;

using NorsynHydraulicCalc;

using System;
using System.Linq;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
public class HydraulicNetworkMsgDto
{
    [Key(0)] public string? Id { get; set; }
    [Key(1)] public DateTime? CalculatedAt { get; set; }
    [Key(2)] public long? CalculationDurationTicks { get; set; }
    [Key(3)] public double TotalPrice { get; set; }
    [Key(4)] public HydraulicSettingsMsgDto? FrozenSettings { get; set; }
    [Key(5)] public UndirectedGraphMsgDto[] Graphs { get; set; } = Array.Empty<UndirectedGraphMsgDto>();
    [Key(6)] public string? Description { get; set; }
    [Key(7)] public BBRMapFeatureMsgDto[]? BbrFeatures { get; set; }

    internal static HydraulicNetworkMsgDto FromDomain(HydraulicNetwork hn)
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
            Description = hn.Description,
            Graphs = hn.Graphs
                .Select(UndirectedGraphMsgDto.FromDomain)
                .ToArray(),
            BbrFeatures = hn.BbrFeatures?.Select(BBRMapFeatureMsgDto.FromDomain).ToArray(),
        };
    }

    internal HydraulicNetwork ToDomain()
    {
        var graphs = Graphs
            .Select(g => g.ToDomain())
            .ToList();

        var frozenSettings = FrozenSettings?.ToDomain();
        var bbrFeatures = BbrFeatures?.Select(b => b.ToDomain()).ToList();

        return HydraulicNetwork.Restore(
            Id,
            graphs,
            frozenSettings,
            CalculatedAt,
            CalculationDurationTicks.HasValue
                ? TimeSpan.FromTicks(CalculationDurationTicks.Value)
                : null,
            TotalPrice,
            Description,
            bbrFeatures);
    }
}
