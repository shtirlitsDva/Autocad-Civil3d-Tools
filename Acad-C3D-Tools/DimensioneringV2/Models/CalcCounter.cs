using MessagePack;

namespace DimensioneringV2.Models;

[MessagePackObject]
internal partial class CalcCounter
{
    [Key(0)] internal int NextId { get; set; } = 1;

    public string Next()
    {
        var id = $"Calc {NextId:D3}";
        NextId++;
        return id;
    }
}
