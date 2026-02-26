using DimensioneringV2.UI;

using DimensioneringV2.UI.MapProperty;
using System.Linq;

namespace DimensioneringV2.Legend
{
    internal static class LegendLabelProvider
    {
        public static string GetLabel(MapPropertyEnum property, object? value)
        {
            if (!MapPropertyMetadata.TryGet(property, out var meta))
                return value?.ToString() ?? "Unknown";

            return GetLabel(meta, value);
        }

        public static string GetLabel(PropertyMeta meta, object? value)
        {
            if (value == null) return "NULL";

            return meta.LegendLabel switch
            {
                LegendLabelFormat.BoolTrueFalse => FormatBoolLabel(value, meta.LegendLabelTemplate),
                LegendLabelFormat.Template => string.Format(meta.LegendLabelTemplate, value),
                LegendLabelFormat.ShowOrFallback => IsEmpty(value)
                    ? meta.LegendLabelFallback
                    : value.ToString() ?? meta.LegendLabelFallback,
                LegendLabelFormat.HideBasicShowRest => IsBasicValue(meta, value)
                    ? ""
                    : value.ToString() ?? "",
                _ => value.ToString() ?? "Unknown"
            };
        }

        private static string FormatBoolLabel(object value, string template)
        {
            var parts = template.Split('|');
            if (value is bool b)
                return b ? parts[0] : (parts.Length > 1 ? parts[1] : "");
            return value.ToString() ?? "";
        }

        private static bool IsEmpty(object? value) => value switch
        {
            null => true,
            string s => string.IsNullOrEmpty(s),
            int i => i == 0,
            _ => false
        };

        private static bool IsBasicValue(PropertyMeta meta, object value)
        {
            var valueStr = value.ToString();
            return meta.BasicStyleValues.Any(bv => valueStr == bv);
        }
    }
}
