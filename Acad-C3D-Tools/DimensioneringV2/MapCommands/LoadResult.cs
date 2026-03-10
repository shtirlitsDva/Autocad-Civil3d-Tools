using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models;
using DimensioneringV2.Serialization;
using DimensioneringV2.Services;

using Microsoft.Win32;

using QuikGraph;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace DimensioneringV2.MapCommands
{
    internal class LoadResult
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
            options.Converters.Add(new DimJsonConverter());
            options.Converters.Add(new AnalysisFeatureJsonConverter());
            options.Converters.Add(new UndirectedGraphJsonConverter());
            return options;
        }

        internal void Execute()
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "D2R Files (*.d2r)|*.d2r|All Files (*.*)|*.*",
                    DefaultExt = "d2r",
                    Title = "Open D2R File",
                    CheckFileExists = true
                };

                string fileName;
                if (openFileDialog.ShowDialog() == true)
                    fileName = openFileDialog.FileName;
                else
                    return;

                if (string.IsNullOrEmpty(fileName)) return;

                if (!File.Exists(fileName))
                {
                    MessageBox.Show("The file does not exist.", "File not found",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string json = File.ReadAllText(fileName);
                HydraulicNetwork hn;

                if (json.TrimStart().StartsWith("{"))
                {
                    var dto = JsonSerializer.Deserialize<HydraulicNetworkDto>(json, s_options);
                    if (dto == null) throw new Exception("Deserialization failed.");
                    hn = dto.ToHydraulicNetwork();
                }
                else
                {
                    var graphs = JsonSerializer.Deserialize<
                        UndirectedGraph<NodeJunction, EdgePipeSegment>[]>(json, s_options);
                    if (graphs == null) throw new Exception("Deserialization failed.");
                    hn = new HydraulicNetwork(graphs.ToList());
                    MessageBox.Show(
                        "Filen er gemt uden indstillinger.\nNuværende indstillinger bruges.",
                        "Legacy format",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                HydraulicNetworkManager.Instance.LoadHn(hn);
                Utils.prtDbg($"Results loaded from {fileName}");
            }
            catch (Exception ex)
            {
                Utils.prtDbg($"An error occurred during loading: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
    }
}