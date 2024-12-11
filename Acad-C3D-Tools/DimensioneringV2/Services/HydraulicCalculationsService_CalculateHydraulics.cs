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

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        
        private static void CalculateHydraulics(
            UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
        {
            HydraulicCalc hc = new(HydraulicSettingsService.Instance.Settings);

            Parallel.ForEach(graph.Edges, edge =>
            {
                var result = hc.CalculateHydraulicSegment(edge.PipeSegment);
                edge.PipeSegment.PipeDim = result.Dim;
                edge.PipeSegment.ReynoldsSupply = result.ReynoldsSupply;
                edge.PipeSegment.ReynoldsReturn = result.ReynoldsReturn;
                edge.PipeSegment.FlowSupply = result.FlowSupply;
                edge.PipeSegment.FlowReturn = result.FlowReturn;
                edge.PipeSegment.PressureGradientSupply = result.PressureGradientSupply;
                edge.PipeSegment.PressureGradientReturn = result.PressureGradientReturn;
                edge.PipeSegment.VelocitySupply = result.VelocitySupply;
                edge.PipeSegment.VelocityReturn = result.VelocityReturn;
                edge.PipeSegment.UtilizationRate = result.UtilizationRate;
            });
        }
    }
}
