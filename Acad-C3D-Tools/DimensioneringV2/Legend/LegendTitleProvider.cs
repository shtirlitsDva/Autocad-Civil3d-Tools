using DimensioneringV2.UI;

using DimensioneringV2.UI.MapProperty;
namespace DimensioneringV2.Legend
{
    internal static class LegendTitleProvider
    {
        public static string GetTitle(MapPropertyEnum property)
        {
            if (property == MapPropertyEnum.Default) return "Ledninger";
            if (property == MapPropertyEnum.Basic) return "";

            if (MapPropertyMetadata.TryGet(property, out var meta))
                return meta.LegendTitle;

            return property.ToString();
        }
    }
}
