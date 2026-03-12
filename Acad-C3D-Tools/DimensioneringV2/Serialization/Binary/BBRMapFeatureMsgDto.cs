using DimensioneringV2.GraphFeatures;

using MessagePack;

using NetTopologySuite.Geometries;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
public class BBRMapFeatureMsgDto
{
    [Key(0)] public string HeatingType { get; set; } = "";
    [Key(1)] public string Address { get; set; } = "";
    [Key(2)] public double OriginalX { get; set; }
    [Key(3)] public double OriginalY { get; set; }

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
