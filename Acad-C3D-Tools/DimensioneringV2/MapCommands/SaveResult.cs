using DimensioneringV2.Serialization;
using DimensioneringV2.Services;

using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace DimensioneringV2.MapCommands
{
    internal class SaveResult
    {
        internal void Execute()
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "D2R Files (*.d2r)|*.d2r|All Files (*.*)|*.*",
                    DefaultExt = "d2r",
                    Title = "Save results",
                    AddExtension = true
                };

                string fileName;
                if (saveFileDialog.ShowDialog() == true)
                {
                    fileName = saveFileDialog.FileName;
                }
                else
                {
                    return;
                }

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

                var graphs = DataService.Instance.Graphs;

                var options = new JsonSerializerOptions();
                options.WriteIndented = true;
                options.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
                options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
                options.Converters.Add(new AnalysisFeatureJsonConverter());
                options.Converters.Add(new UndirectedGraphJsonConverter());
                options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

                string json = JsonSerializer.Serialize(graphs.ToArray(), options);
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
