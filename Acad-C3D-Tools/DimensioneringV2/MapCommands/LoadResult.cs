using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Serialization;
using DimensioneringV2.Services;

using Microsoft.Win32;

using QuikGraph;

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
    internal class LoadResult
    {
        internal void Execute()
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "D2R Files (*.d2r)|*.d2r|All Files (*.*)|*.*",
                    DefaultExt = "d2r",
                    Title = "Open D2R File",
                    CheckFileExists = true // Ensures the user selects an existing file
                };

                string fileName;
                if (openFileDialog.ShowDialog() == true)
                {
                    fileName = openFileDialog.FileName;
                }
                else
                {
                    return;
                }

                if (string.IsNullOrEmpty(fileName)) return;

                if (!File.Exists(fileName))
                {
                    MessageBox.Show("The file does not exist.", "File not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var options = new JsonSerializerOptions();
                options.WriteIndented = true;
                options.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
                options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
                options.Converters.Add(new DimJsonConverter());
                options.Converters.Add(new AnalysisFeatureJsonConverter());
                options.Converters.Add(new UndirectedGraphJsonConverter());
                options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                var graphs = JsonSerializer.Deserialize<UndirectedGraph<NodeJunction, EdgePipeSegment>[]>(
                    File.ReadAllText(fileName), options);
                if (graphs == null) throw new System.Exception("Deserialization failed.");
                DataService.Instance.LoadSavedResultsData(graphs);

                Utils.prtDbg($"Results loaded from {fileName}");
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during loading: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
    }
    //mathias var her
}