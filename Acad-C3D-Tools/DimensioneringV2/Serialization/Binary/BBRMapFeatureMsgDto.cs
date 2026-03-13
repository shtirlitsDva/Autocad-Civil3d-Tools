using DimensioneringV2.GraphFeatures;

using MessagePack;

using NetTopologySuite.Geometries;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal partial class BBRMapFeatureMsgDto
{
    [Key(0)] internal string HeatingType { get; set; } = "";
    [Key(1)] internal string Address { get; set; } = "";
    [Key(2)] internal double OriginalX { get; set; }
    [Key(3)] internal double OriginalY { get; set; }

    internal static BBRMapFeatureMsgDto FromDomain(BBRMapFeature feature) => new()
    {
        HeatingType = feature.HeatingType,
        Address = feature.Address,
        OriginalX = feature.OriginalX,
        OriginalY = feature.OriginalY,
    };

    internal BBRMapFeature ToDomain()
    {
        var geometry = new Point(OriginalX, OriginalY);
        return new BBRMapFeature(geometry, HeatingType, Address, OriginalX, OriginalY);
    }
}
