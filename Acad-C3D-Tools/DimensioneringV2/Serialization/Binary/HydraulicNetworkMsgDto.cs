using DimensioneringV2.Models;

using MessagePack;

using NorsynHydraulicCalc;

using System;
using System.Linq;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal partial class HydraulicNetworkMsgDto
{
    [Key(0)] internal string? Id { get; set; }
    [Key(1)] internal DateTime? CalculatedAt { get; set; }
    [Key(2)] internal long? CalculationDurationTicks { get; set; }
    [Key(3)] internal double TotalPrice { get; set; }
    [Key(4)] internal HydraulicSettingsMsgDto? FrozenSettings { get; set; }
    [Key(5)] internal UndirectedGraphMsgDto[] Graphs { get; set; } = Array.Empty<UndirectedGraphMsgDto>();
    [Key(6)] internal string? Description { get; set; }
    [Key(7)] internal BBRMapFeatureMsgDto[]? BbrFeatures { get; set; }
    [Key(8)] internal NyttetimerConfigurationMsgDto? FrozenNyttetimerConfig { get; set; }

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
            FrozenNyttetimerConfig = hn.FrozenNyttetimerConfig != null
                ? NyttetimerConfigurationMsgDto.FromDomain(hn.FrozenNyttetimerConfig)
                : null,
        };
    }

    internal HydraulicNetwork ToDomain()
    {
        var graphs = Graphs
            .Select(g => g.ToDomain())
            .ToList();

        var frozenSettings = FrozenSettings?.ToDomain();
        var bbrFeatures = BbrFeatures?.Select(b => b.ToDomain()).ToList();
        var frozenNyttetimerConfig = FrozenNyttetimerConfig?.ToDomain();

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
            bbrFeatures,
            frozenNyttetimerConfig);
    }
}
