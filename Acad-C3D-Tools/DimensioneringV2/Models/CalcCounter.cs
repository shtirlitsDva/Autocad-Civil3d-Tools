namespace DimensioneringV2.Models;

internal class CalcCounter
{
    public int NextId { get; set; } = 1;

    public string Next()
    {
        var id = $"Calc {NextId:D3}";
        NextId++;
        return id;
    }
}
