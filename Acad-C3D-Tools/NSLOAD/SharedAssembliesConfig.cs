using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NSLOAD
{
    public class SharedAssembliesConfig
    {
        public List<string> SharedAssemblies { get; set; } = new();
        public List<string> MixedModeAssemblies { get; set; } = new();
        // Subset of SharedAssemblies loaded via Assembly.Load(byte[]) instead
        // of Assembly.LoadFrom. Releases the file lock so the developer can
        // overwrite the DLL on disk; the running image stays in the default
        // ALC until AutoCAD restarts. Mutually exclusive with
        // MixedModeAssemblies (native deps need directory probing, which the
        // streamed/location-unknown path cannot provide).
        public List<string> StreamedAssemblies { get; set; } = new();
    }

    public static class SharedAssembliesConfigLoader
    {
        private const string FileName = "SharedAssemblies.Config.json";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static SharedAssembliesConfig Load(string pluginDir)
        {
            string path = Path.Combine(pluginDir, FileName);
            if (!File.Exists(path))
                return new SharedAssembliesConfig();

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<SharedAssembliesConfig>(
                    json, _jsonOptions) ?? new SharedAssembliesConfig();
            }
            catch
            {
                return new SharedAssembliesConfig();
            }
        }
    }
}
