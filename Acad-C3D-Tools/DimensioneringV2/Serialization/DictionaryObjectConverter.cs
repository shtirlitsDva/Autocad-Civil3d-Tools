using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DimensioneringV2.Serialization
{
    class DictionaryObjectConverter
    {
        public static Dictionary<string, object> ConvertAttributesToTyped(
            Dictionary<string, JsonElement> attributes, Type modelType, JsonSerializerOptions? options = null)
        {
            var typedAttributes = new Dictionary<string, object>();

            var props = modelType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite || p.GetSetMethod(true) != null || p.CanRead)
                .Where(p => p.GetIndexParameters().Length == 0); // skip indexers!;

            IEnumerable<IGrouping<string, PropertyInfo>> dups = props                
                .GroupBy(a => a.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g);

            if (dups.Count() > 0)
                throw new InvalidDataException($"Duplicate attribute(s): " +
                    $"{string.Join(", ", dups.Select(x => x.Key))}");

            var propertyTypes = props  // read-only props also useful for matching                
                .ToDictionary(p => p.Name, p => p.PropertyType, StringComparer.OrdinalIgnoreCase);

            foreach (var (key, jsonElement) in attributes)
            {
                if (propertyTypes.TryGetValue(key, out var propertyType))
                {
                    try
                    {
                        var deserialized = jsonElement.Deserialize(propertyType, options);
                        if (deserialized != null)
                            typedAttributes[key] = deserialized;
                        continue;
                    }
                    catch
                    {
                        // Fall back to default if deserialization fails
                    }
                }

                // Fallback for unknown fields
                typedAttributes[key] = FallbackConvert(jsonElement);
            }

            return typedAttributes;
        }
        private static object FallbackConvert(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number => element.TryGetInt64(out var i) ? i :
                                        element.TryGetDouble(out var d) ? d : element.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                _ => element.GetRawText()
            };
        }
    }
}
