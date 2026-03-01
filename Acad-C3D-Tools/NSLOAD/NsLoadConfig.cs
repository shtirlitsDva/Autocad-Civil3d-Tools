using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSLOAD
{
    public class NsLoadConfig
    {
        public List<UserPluginEntry> Plugins { get; set; } = new();
        public List<PredefinedAppEntry> PredefinedApps { get; set; } = new();
    }

    public class UserPluginEntry
    {
        public string Name { get; set; } = "";
        public string DllPath { get; set; } = "";
        public bool LoadOnStartup { get; set; }
    }

    public class PredefinedAppEntry
    {
        public string DisplayName { get; set; } = "";
        public bool AutoLoad { get; set; }
    }

    public static class NsLoadConfigLoader
    {
        private const string AppFolder = "NSLOAD";
        private const string ConfigFileName = "config.json";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, AppFolder, ConfigFileName);
        }

        public static NsLoadConfig? Load()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<NsLoadConfig>(json, _jsonOptions);
        }

        public static void Save(NsLoadConfig config)
        {
            string path = GetConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(path, json);
        }

        public static NsLoadConfig MergeWithCsv(
            NsLoadConfig? config, Dictionary<string, string> csvApps)
        {
            config ??= new NsLoadConfig();

            var existing = config.PredefinedApps
                .ToDictionary(a => a.DisplayName, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in csvApps)
            {
                if (!existing.ContainsKey(kvp.Key))
                {
                    config.PredefinedApps.Add(new PredefinedAppEntry
                    {
                        DisplayName = kvp.Key,
                        AutoLoad = false,
                    });
                }
            }

            config.PredefinedApps.RemoveAll(a =>
                !csvApps.ContainsKey(a.DisplayName));

            return config;
        }
    }
}
