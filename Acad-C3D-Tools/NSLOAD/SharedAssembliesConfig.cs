using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NSLOAD
{
    public class SharedAssembliesConfig
    {
        public List<string> SharedAssemblies { get; set; } = new();
    }

    public static class SharedAssembliesConfigLoader
    {
        private const string FileName = "SharedAssemblies.Config.json";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static string[] Load(string pluginDir)
        {
            string path = Path.Combine(pluginDir, FileName);
            if (!File.Exists(path))
                return Array.Empty<string>();

            try
            {
                string json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<SharedAssembliesConfig>(
                    json, _jsonOptions);
                return config?.SharedAssemblies?.ToArray() ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
