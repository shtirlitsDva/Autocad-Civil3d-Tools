using System;
using System.Collections.Generic;
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

            var propertyTypes = modelType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite || p.GetSetMethod(true) != null || p.CanRead) // read-only props also useful for matching
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
