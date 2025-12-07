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
using DimensioneringV2.ResultCache;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static void BFCalcHydraulics(
            UndirectedGraph<BFNode, BFEdge> graph, HydraulicCalculationCache cache)
        {
            Parallel.ForEach(graph.Edges, edge =>
            {
                CalculationResultClient result;
                if (edge.SegmentType == SegmentType.Stikledning)
                {
                    result = cache.GetServicePipeResult(edge.OriginalEdge.PipeSegment);
                }
                else
                {
                    result = cache.GetOrCalculateSupplyPipeResult(edge);
                    if (result.Dim == null)
                        throw new Exception(
                            $"Pipe dimension is null for edge {edge.Source.Location} -> {edge.Target.Location}");
                }                    
                edge.Dim = result.Dim;
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
