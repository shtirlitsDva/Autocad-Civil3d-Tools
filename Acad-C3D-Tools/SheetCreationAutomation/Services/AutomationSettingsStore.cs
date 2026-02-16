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
        private static string SheetsStatePath => Path.Combine(SettingsFolder, "sheets-ui-state.json");
        private static string FinalizeStatePath => Path.Combine(SettingsFolder, "finalize-ui-state.json");

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

        public static SheetsUiState LoadSheetsState()
        {
            try
            {
                if (!File.Exists(SheetsStatePath))
                {
                    return new SheetsUiState();
                }

                string json = File.ReadAllText(SheetsStatePath);
                return JsonSerializer.Deserialize<SheetsUiState>(json) ?? new SheetsUiState();
            }
            catch
            {
                return new SheetsUiState();
            }
        }

        public static void SaveSheetsState(SheetsUiState state)
        {
            Directory.CreateDirectory(SettingsFolder);
            string json = JsonSerializer.Serialize(state, WriteOptions);
            File.WriteAllText(SheetsStatePath, json);
        }

        public static FinalizeUiState LoadFinalizeState()
        {
            try
            {
                if (!File.Exists(FinalizeStatePath))
                {
                    return new FinalizeUiState();
                }

                string json = File.ReadAllText(FinalizeStatePath);
                return JsonSerializer.Deserialize<FinalizeUiState>(json) ?? new FinalizeUiState();
            }
            catch
            {
                return new FinalizeUiState();
            }
        }

        public static void SaveFinalizeState(FinalizeUiState state)
        {
            Directory.CreateDirectory(SettingsFolder);
            string json = JsonSerializer.Serialize(state, WriteOptions);
            File.WriteAllText(FinalizeStatePath, json);
        }
    }
}
