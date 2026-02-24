namespace DimensioneringV2.Legend
{
    /// <summary>
    /// Implemented by theme classes to build their legend panel declaratively.
    /// Replaces the old <c>ILegendData</c> interface.
    /// </summary>
    internal interface ILegendSource
    {
        LegendElement? BuildLegendPanel();
    }
}
