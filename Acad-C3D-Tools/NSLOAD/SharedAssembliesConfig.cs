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

        // Optional per-assembly external source directory (name -> dir). When an entry
        // is present the loader resolves THAT assembly from this dir instead of the
        // plugin dir, so a plugin can load a shared/interop assembly straight from its
        // csproj HintPath location (e.g. Appload) with no private copy. Absent/empty ⇒
        // resolve from the plugin dir (original behaviour).
        public Dictionary<string, string> AssemblyLocations { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
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
                var cfg = JsonSerializer.Deserialize<SharedAssembliesConfig>(
                    json, _jsonOptions) ?? new SharedAssembliesConfig();
                // Normalise to case-insensitive so name lookups in LoadCore match
                // regardless of key casing (or if the field is absent).
                cfg.AssemblyLocations = cfg.AssemblyLocations == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(cfg.AssemblyLocations, StringComparer.OrdinalIgnoreCase);
                return cfg;
            }
            catch
            {
                return new SharedAssembliesConfig();
            }
        }
    }
}
