using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.ShortestPath;

using DimensioneringV2.GraphFeatures;
using System.Diagnostics;

using utils = IntersectUtilities.UtilsCommon.Utils;
using System.IO;
using NorsynHydraulicCalc;
using DimensioneringV2.BruteForceOptimization;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        
        private static void BFCalcHydraulics(
            UndirectedGraph<BFNode, BFEdge> graph)
        {
            HydraulicCalc hc = new(HydraulicSettingsService.Instance.Settings);

            Parallel.ForEach(graph.Edges, edge =>
            {
                var result = hc.CalculateHydraulicSegment(edge);
                edge.PipeDim = result.Dim;
                edge.ReynoldsSupply = result.ReynoldsSupply;
                edge.ReynoldsReturn = result.ReynoldsReturn;
                edge.FlowSupply = result.FlowSupply;
                edge.FlowReturn = result.FlowReturn;
                edge.PressureGradientSupply = result.PressureGradientSupply;
                edge.PressureGradientReturn = result.PressureGradientReturn;
                edge.VelocitySupply = result.VelocitySupply;
                edge.VelocityReturn = result.VelocityReturn;
                edge.UtilizationRate = result.UtilizationRate;
            });
        }
    }
}
