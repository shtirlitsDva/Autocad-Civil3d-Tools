using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GDALService.Protocol
{
    internal static class Json
    {
        internal static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static T Extract<T>(object? payload)
        {
            return payload switch
            {
                null => throw new ArgumentNullException(nameof(payload)),
                JsonElement e => e.Deserialize<T>(Options)!,
                string s => System.Text.Json.JsonSerializer.Deserialize<T>(s, Options)!,
                _ => (T)payload
            };
        }

        public static void Write(object obj)
        {
            var json = JsonSerializer.Serialize(obj, Options);
            Console.WriteLine(json);
            Console.Out.Flush();
        }
    }
}
