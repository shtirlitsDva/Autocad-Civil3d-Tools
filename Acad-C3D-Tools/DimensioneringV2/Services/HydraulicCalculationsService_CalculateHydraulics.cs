using DimensioneringV2.AutoCAD;
using DimensioneringV2.GraphFeatures;

using NorsynHydraulicCalc;

using QuikGraph;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal void CalculateGraphs(
            IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs)
        {
            NorsynHydraulicCalc.HydraulicCalc hc;
            if (HydraulicSettingsService.Instance.Settings.ReportToConsole)
            {
                hc = new HydraulicCalc(
                HydraulicSettingsService.Instance.Settings,
                new LoggerFile());
            }
            else
            {
                hc = new HydraulicCalc(
                HydraulicSettingsService.Instance.Settings,
                new LoggerAcConsole());
            }
            Parallel.ForEach(graphs, graph => CalculateGraph(graph, hc));
        }

        internal void CalculateGraph(
            UndirectedGraph<NodeJunction, EdgePipeSegment> graph, NorsynHydraulicCalc.HydraulicCalc hc)
        {
            foreach (var edge in graph.Edges)
            {
                if (edge.PipeSegment.NumberOfBuildingsSupplied == 0) continue;

                var result = hc.CalculateHydraulicSegment(edge.PipeSegment);
                edge.PipeSegment.Dim = result.Dim;
                edge.PipeSegment.ReynoldsSupply = result.ReynoldsSupply;
                edge.PipeSegment.ReynoldsReturn = result.ReynoldsReturn;
                edge.PipeSegment.DimFlowSupply = result.FlowSupply;
                edge.PipeSegment.DimFlowReturn = result.FlowReturn;
                edge.PipeSegment.PressureGradientSupply = result.PressureGradientSupply;
                edge.PipeSegment.PressureGradientReturn = result.PressureGradientReturn;
                edge.PipeSegment.VelocitySupply = result.VelocitySupply;
                edge.PipeSegment.VelocityReturn = result.VelocityReturn;
                edge.PipeSegment.UtilizationRate = result.UtilizationRate;
            }
        }
    }
}
