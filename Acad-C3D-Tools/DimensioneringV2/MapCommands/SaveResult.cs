using DimensioneringV2.Serialization;
using DimensioneringV2.Services;

using Microsoft.Win32;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace DimensioneringV2.MapCommands
{
    internal class SaveResult
    {
        private static readonly JsonSerializerOptions s_options = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                ReferenceHandler = ReferenceHandler.Preserve,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            options.Converters.Add(new AnalysisFeatureJsonConverter());
            options.Converters.Add(new UndirectedGraphJsonConverter());
            options.Converters.Add(new DimJsonConverter());
            return options;
        }

        internal void Execute()
        {
            try
            {
                var hn = HydraulicNetworkManager.Instance.ActiveNetwork;
                if (hn == null) return;

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "D2R Files (*.d2r)|*.d2r|All Files (*.*)|*.*",
                    DefaultExt = "d2r",
                    Title = "Save results",
                    AddExtension = true,
                    FileName = hn.Id ?? "result"
                };

                string fileName;
                if (saveFileDialog.ShowDialog() == true)
                    fileName = saveFileDialog.FileName;
                else
                    return;

                if (string.IsNullOrEmpty(fileName)) return;

                if (File.Exists(fileName))
                {
                    MessageBoxResult result = MessageBox.Show(
                        "The file already exists. Do you want to overwrite it?",
                        "File already exists",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes) return;
                }

                var dto = new HydraulicNetworkDto(hn);
                string json = JsonSerializer.Serialize(dto, s_options);
                File.WriteAllText(fileName, json);

                Utils.prtDbg($"Results saved to {fileName}");
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during saving: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
    }
}
