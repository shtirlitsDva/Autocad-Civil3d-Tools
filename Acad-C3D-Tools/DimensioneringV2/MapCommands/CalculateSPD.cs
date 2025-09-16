using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.Services.SubGraphs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class CalculateSPD
    {
        internal async Task Execute()
        {
            try
            {
                await Task.Run(() => HydraulicCalculationsService.CalculateSPDijkstra(
                    new List<(Func<AnalysisFeature, dynamic> Getter, Action<AnalysisFeature, dynamic> Setter)>
                    {
                        (f => f.NumberOfBuildingsConnected, (f, v) => f.NumberOfBuildingsSupplied = v),
                        (f => f.NumberOfUnitsConnected, (f, v) => f.NumberOfUnitsSupplied = v),
                        (f => f.HeatingDemandConnected, (f, v) => f.HeatingDemandSupplied = v)
                    }));

                var graphs = DataService.Instance.Graphs;

                //Perform post processing
                foreach (var graph in graphs)
                {
                    PressureAnalysisService.CalculateDifferentialLossAtClient(graph);
                }
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during calculations: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
    }
}