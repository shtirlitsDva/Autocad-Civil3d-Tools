using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace AcadOverrules.VertexCircles
{
    /// <summary>
    /// Reads and writes <see cref="VertexCirclesConfig"/> as JSON under the roaming profile.
    /// A missing or unreadable file yields the default config rather than an error - losing
    /// the settings must never stop the overrule from drawing.
    /// </summary>
    internal static class VertexCirclesSettingsStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter() },
        };

        private static string SettingsFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Norsyn", "AcadOverrules");

        public static string SettingsPath =>
            Path.Combine(SettingsFolder, "polyline-vertex-circles.json");

        public static VertexCirclesConfig Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return VertexCirclesConfig.CreateDefault();

                var config = JsonSerializer.Deserialize<VertexCirclesConfig>(
                    File.ReadAllText(SettingsPath), Options);

                if (config == null || config.Profiles.Count == 0)
                    return VertexCirclesConfig.CreateDefault();

                return config;
            }
            catch (System.Exception)
            {
                return VertexCirclesConfig.CreateDefault();
            }
        }

        public static void Save(VertexCirclesConfig config)
        {
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(config, Options));
        }
    }
}
