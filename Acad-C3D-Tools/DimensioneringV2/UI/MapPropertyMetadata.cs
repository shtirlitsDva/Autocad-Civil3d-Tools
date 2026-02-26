using DimensioneringV2.GraphFeatures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DimensioneringV2.UI
{
    internal class PropertyMeta
    {
        public PropertyInfo PropertyInfo { get; }
        public MapPropertyAttribute Attribute { get; }

        public MapPropertyEnum Enum => Attribute.Property;
        public string PropertyName => PropertyInfo.Name;
        public ThemeKind ThemeKind => Attribute.Theme;
        public string Description => Attribute.Description;
        public string LegendTitle => Attribute.LegendTitle;
        public string[] BasicStyleValues => Attribute.BasicStyleValues;
        public string DisplayValuePath => Attribute.DisplayValuePath;
        public string OrderingPropertyPath => Attribute.OrderingPropertyPath;
        public LabelFormat LabelFormat => Attribute.LabelFormat;
        public LegendLabelFormat LegendLabel => Attribute.LegendLabel;
        public string LegendLabelTemplate => Attribute.LegendLabelTemplate;
        public string LegendLabelFallback => Attribute.LegendLabelFallback;

        private PropertyInfo? _subPropertyInfo;
        private bool _subPropertyResolved;

        private PropertyInfo? _orderingPropertyInfo;
        private bool _orderingPropertyResolved;

        public PropertyMeta(PropertyInfo prop, MapPropertyAttribute attr)
        {
            PropertyInfo = prop;
            Attribute = attr;
        }

        /// <summary>
        /// Resolves the display value for this property from a feature,
        /// following DisplayValuePath if set.
        /// </summary>
        public object? ResolveDisplayValue(AnalysisFeature feature)
        {
            var value = PropertyInfo.GetValue(feature);

            if (string.IsNullOrEmpty(DisplayValuePath) || value == null)
                return value;

            var subProp = GetSubPropertyInfo(value.GetType());
            return subProp?.GetValue(value);
        }

        /// <summary>
        /// Gets the ordering value for a feature.
        /// Used when OrderingPropertyPath is set (e.g., Dim.OrderingPriority).
        /// Returns null if no ordering path is configured.
        /// </summary>
        public object? GetOrderingValue(AnalysisFeature feature)
        {
            if (string.IsNullOrEmpty(OrderingPropertyPath)) return null;

            var value = PropertyInfo.GetValue(feature);
            if (value == null) return null;

            var orderProp = GetOrderingPropertyInfo(value.GetType());
            return orderProp?.GetValue(value);
        }

        /// <summary>
        /// Converts BasicStyleValues strings to properly typed objects,
        /// matching the resolved display value type.
        /// </summary>
        public object[] GetTypedBasicStyleValues()
        {
            if (BasicStyleValues.Length == 0) return [];

            var propType = GetResolvedType();
            return BasicStyleValues
                .Select(s => ConvertFromString(s, propType))
                .ToArray();
        }

        private Type GetResolvedType()
        {
            if (!string.IsNullOrEmpty(DisplayValuePath))
            {
                var subProp = PropertyInfo.PropertyType.GetProperty(DisplayValuePath);
                if (subProp != null) return subProp.PropertyType;
            }
            return PropertyInfo.PropertyType;
        }

        private PropertyInfo? GetSubPropertyInfo(Type parentType)
        {
            if (!_subPropertyResolved)
            {
                _subPropertyInfo = parentType.GetProperty(DisplayValuePath);
                _subPropertyResolved = true;
            }
            return _subPropertyInfo;
        }

        private PropertyInfo? GetOrderingPropertyInfo(Type parentType)
        {
            if (!_orderingPropertyResolved)
            {
                _orderingPropertyInfo = parentType.GetProperty(OrderingPropertyPath);
                _orderingPropertyResolved = true;
            }
            return _orderingPropertyInfo;
        }

        private static object ConvertFromString(string s, Type target)
        {
            if (target == typeof(bool)) return bool.Parse(s);
            if (target == typeof(int)) return int.Parse(s);
            if (target == typeof(double)) return double.Parse(s);
            if (target == typeof(string)) return s;
            return Convert.ChangeType(s, target);
        }
    }

    internal static class MapPropertyMetadata
    {
        private static readonly Lazy<IReadOnlyDictionary<MapPropertyEnum, PropertyMeta>> _cache
            = new(() => Build());

        public static IReadOnlyDictionary<MapPropertyEnum, PropertyMeta> All => _cache.Value;

        public static PropertyMeta Get(MapPropertyEnum prop)
        {
            if (All.TryGetValue(prop, out var meta)) return meta;
            throw new KeyNotFoundException(
                $"MapPropertyEnum '{prop}' not found. " +
                $"Ensure the property is decorated with [MapProperty({prop})] on AnalysisFeature.");
        }

        public static bool TryGet(MapPropertyEnum prop, out PropertyMeta meta)
            => All.TryGetValue(prop, out meta!);

        public static IEnumerable<PropertyMeta> Themed =>
            All.Values.Where(m => m.ThemeKind != ThemeKind.None);

        private static Dictionary<MapPropertyEnum, PropertyMeta> Build()
        {
            return typeof(AnalysisFeature)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => (prop: p, attr: p.GetCustomAttribute<MapPropertyAttribute>()))
                .Where(x => x.attr != null)
                .ToDictionary(
                    x => x.attr!.Property,
                    x => new PropertyMeta(x.prop, x.attr!));
        }
    }
}
