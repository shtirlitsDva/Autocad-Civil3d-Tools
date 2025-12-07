using DimensioneringV2.AutoCAD;
using DimensioneringV2.BruteForceOptimization;

using NorsynHydraulicCalc;

using QuikGraph;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal void CalculateGraphs(
            IEnumerable<UndirectedGraph<BFNode, BFEdge>> graphs)
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
            Parallel.ForEach(graphs, graph => CalculateGraphsFordelingsledninger(graph, hc));
        }

        /// <summary>
        /// SERVICE (Stikledninger) should be precalculated and results stored in DTOs.
        /// </summary>        
        internal void CalculateGraphsFordelingsledninger(
            UndirectedGraph<BFNode, BFEdge> graph, NorsynHydraulicCalc.HydraulicCalc hc)
        {
            foreach (var edge in graph.Edges)
            {
                if (edge.SegmentType != SegmentType.Fordelingsledning) continue;
                if (edge.NumberOfBuildingsSupplied == 0) continue;

                var result = hc.CalculateDistributionSegment(edge);
                edge.ApplyResult(result);
            }
        }
    }
}
