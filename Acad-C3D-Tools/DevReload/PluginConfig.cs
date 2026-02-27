using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevReload
{
    public class PluginConfig
    {
        public List<PluginEntry> Plugins { get; set; } = new();
    }

    public class PluginEntry
    {
        public string Name { get; set; } = "";
        public string? DllPath { get; set; }
        public string? VsProject { get; set; }
        public bool Commands { get; set; }
        public string? CommandPrefix { get; set; }
        public bool LoadOnStartup { get; set; }
        public int PaletteWidth { get; set; } = 400;
        public int PaletteHeight { get; set; } = 600;
        public string DockSide { get; set; } = "Right";
    }

    public static class PluginConfigLoader
    {
        private const string ConfigFileName = "plugins.json";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string GetConfigPath()
        {
            string loaderDir = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location)!;
            return Path.Combine(loaderDir, ConfigFileName);
        }

        /// <summary>
        /// Load the plugin config from plugins.json next to DevReload.dll.
        /// Returns null if the file does not exist.
        /// </summary>
        public static PluginConfig? Load()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PluginConfig>(json, _jsonOptions);
        }

        /// <summary>
        /// Save the plugin config to plugins.json. Creates the file if it
        /// doesn't exist, overwrites if it does.
        /// </summary>
        public static void Save(PluginConfig config)
        {
            string path = GetConfigPath();
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(path, json);
        }
    }
}
