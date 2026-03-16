using DimensioneringV2.Models.Nyttetimer;

using MessagePack;

using System;
using System.Linq;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal class NyttetimerConfigurationMsgDto
{
    [Key(0)] internal string Name { get; set; } = "";
    [Key(1)] internal NyttetimerEntryMsgDto[] Entries { get; set; } = Array.Empty<NyttetimerEntryMsgDto>();

    internal static NyttetimerConfigurationMsgDto FromDomain(NyttetimerConfigurationData data)
    {
        return new NyttetimerConfigurationMsgDto
        {
            Name = data.Name,
            Entries = data.Entries
                .Select(e => new NyttetimerEntryMsgDto
                {
                    AnvendelsesKode = e.AnvendelsesKode,
                    Nyttetimer = e.Nyttetimer
                })
                .ToArray()
        };
    }

    internal NyttetimerConfigurationData ToDomain()
    {
        return new NyttetimerConfigurationData
        {
            Name = Name,
            Entries = Entries
                .Select(e => new NyttetimerEntryData
                {
                    AnvendelsesKode = e.AnvendelsesKode,
                    Nyttetimer = e.Nyttetimer
                })
                .ToList()
        };
    }
}

[MessagePackObject]
internal class NyttetimerEntryMsgDto
{
    [Key(0)] internal string AnvendelsesKode { get; set; } = "";
    [Key(1)] internal int Nyttetimer { get; set; }
}
