using SheetCreationAutomation.Models;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace SheetCreationAutomation.Services
{
    internal static class AutomationSettingsStore
    {
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        private static string SettingsFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DRI", "SheetCreationAutomation");

        private static string ViewFramesStatePath => Path.Combine(SettingsFolder, "viewframes-ui-state.json");

        public static ViewFramesUiState LoadViewFramesState()
        {
            try
            {
                if (!File.Exists(ViewFramesStatePath))
                {
                    return new ViewFramesUiState();
                }

                string json = File.ReadAllText(ViewFramesStatePath);
                return JsonSerializer.Deserialize<ViewFramesUiState>(json) ?? new ViewFramesUiState();
            }
            catch
            {
                return new ViewFramesUiState();
            }
        }

        public static void SaveViewFramesState(ViewFramesUiState state)
        {
            Directory.CreateDirectory(SettingsFolder);
            string json = JsonSerializer.Serialize(state, WriteOptions);
            File.WriteAllText(ViewFramesStatePath, json);
        }
    }
}
