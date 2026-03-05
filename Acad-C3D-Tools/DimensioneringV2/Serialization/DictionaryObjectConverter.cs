using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace DimensioneringV2.Serialization
{
    class DictionaryObjectConverter
    {
        public static Dictionary<string, object> ConvertAttributesToTyped(
            Dictionary<string, JsonElement> attributes,
            Type modelType,
            JsonSerializerOptions? options = null)
        {
            var typedAttributes = new Dictionary<string, object>();

            var props = modelType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite || p.GetSetMethod(true) != null || p.CanRead)
                .Where(p => p.GetIndexParameters().Length == 0);

            IEnumerable<IGrouping<string, PropertyInfo>> dups = props
                .GroupBy(a => a.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g);

            if (dups.Count() > 0)
                throw new InvalidDataException($"Duplicate attribute(s): " +
                    $"{string.Join(", ", dups.Select(x => x.Key))}");

            var propertyTypes = props
                .ToDictionary(p => p.Name, p => p.PropertyType, StringComparer.OrdinalIgnoreCase);

            foreach (var (key, jsonElement) in attributes)
            {
                if (propertyTypes.TryGetValue(key, out var propertyType))
                {
                    try
                    {
                        var deserialized = jsonElement.Deserialize(propertyType, options);
                        if (deserialized != null && !IsDefault(deserialized))
                            typedAttributes[key] = deserialized;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to deserialize '{key}' as {propertyType.Name}. " +
                            $"JSON: '{jsonElement.GetRawText()}'.", ex);
                    }
                }

                throw new InvalidOperationException(
                    $"Unknown attribute '{key}' (value: '{jsonElement.GetRawText()}') " +
                    $"has no matching property on {modelType.Name}. " +
                    $"Add a property to {modelType.Name} for this key.");
            }

            return typedAttributes;
        }

        private static bool IsDefault(object? value) => value switch
        {
            null => true,
            string s => s.Length == 0,
            int i => i == 0,
            double d => d == 0.0,
            bool b => !b,
            _ => false,
        };
    }
}
