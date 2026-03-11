using MessagePack;

namespace DimensioneringV2.Models;

[MessagePackObject]
internal class CalcCounter
{
    [Key(0)] public int NextId { get; set; } = 1;

    public string Next()
    {
        var id = $"Calc {NextId:D3}";
        NextId++;
        return id;
    }
}
