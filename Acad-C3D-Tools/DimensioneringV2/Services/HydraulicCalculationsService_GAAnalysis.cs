using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.ShortestPath;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.BruteForceOptimization;
using System.Diagnostics;

using utils = IntersectUtilities.UtilsCommon.Utils;
using System.IO;
using NorsynHydraulicCalc;
using DimensioneringV2.SteinerTreeProblem;
using System.Security.Policy;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon;
using System.Collections.Concurrent;

using DimensioneringV2.Genetic;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static void CalculateGAAnalysis(
            List<(
                Func<BFEdge, dynamic> Getter,
                Action<BFEdge, dynamic> Setter)> props)
        {
            var graphs = _dataService.Graphs;

            //Reset the results
            foreach (var f in graphs.SelectMany(g => g.Edges.Select(e => e.PipeSegment))) f.ResetHydraulicResults();

            foreach (UndirectedGraph<NodeJunction, EdgePipeSegment> graph in graphs)
            {
                UndirectedGraph<BFNode, BFEdge> bfGraph = graph.CopyToBF();

                bool optimizationContinues = true;
                int optimizationCounter = 0;
                while (optimizationContinues)
                {
                    if (VM.StopRequested) break;

                    optimizationCounter++;
                    
                    var bridges = FindBridges.DoFindThem(bfGraph);
                    var nonBridges = bfGraph.Edges.Where(x => !bridges.Contains(x));

                    VM.UpdateBridges(bridges.Count);

                    if (bridges.Count == bfGraph.Edges.Count())
                    {
                        optimizationContinues = false;
                        break;
                    }

                    var chromosomeLenght = nonBridges.Count();
                    var fitness = new GraphChromosome();



                    var results = new ConcurrentBag<(UndirectedGraph<BFNode, BFEdge> graph, double cost)>();

                    int counter = 0;
                    Parallel.ForEach(nonBridges, candidate =>
                    {
                        counter++;
                        VM.UpdateCurrentCandidate(counter);
                        var cGraph = bfGraph.Copy();
                        var cCandidate = cGraph.Edges.First(
                            x => x.Source.OriginalNodeJunction == candidate.Source.OriginalNodeJunction &&
                            x.Target.OriginalNodeJunction == candidate.Target.OriginalNodeJunction);
                        cGraph.RemoveEdge(cCandidate);

                        double cost = CalculateBFCost(cGraph, props);
                        //double cost = cGraph.Edges.Sum(x => x.Length);
                        results.Add((cGraph, cost));
                    });

                    var bestResult = results.MinBy(x => x.cost);
                    bfGraph = bestResult.graph;
                }

                //If running length, calculate hydraulic results
                //CalculateBFCost(bfGraph, props);

                //Update the original graph with the results from the best result
                foreach (var edge in bfGraph.Edges)
                {
                    var originalEdge = graph.Edges.FirstOrDefault(
                        x => x.Source == edge.Source.OriginalNodeJunction &&
                        x.Target == edge.Target.OriginalNodeJunction);
                    if (originalEdge == null)
                    {
                        originalEdge = graph.Edges.FirstOrDefault(
                            x => x.Source == edge.Target.OriginalNodeJunction &&
                            x.Target == edge.Source.OriginalNodeJunction);
                    }
                    if (originalEdge == null)
                    {
                        utils.prdDbg($"Couldn't find original edge for {edge.Source} -> {edge.Target}");
                        continue;
                    }
                    originalEdge.PipeSegment.PipeDim = edge.PipeDim;
                    originalEdge.PipeSegment.ReynoldsSupply = edge.ReynoldsSupply;
                    originalEdge.PipeSegment.ReynoldsReturn = edge.ReynoldsReturn;
                    originalEdge.PipeSegment.FlowSupply = edge.FlowSupply;
                    originalEdge.PipeSegment.FlowReturn = edge.FlowReturn;
                    originalEdge.PipeSegment.PressureGradientSupply = edge.PressureGradientSupply;
                    originalEdge.PipeSegment.PressureGradientReturn = edge.PressureGradientReturn;
                    originalEdge.PipeSegment.VelocitySupply = edge.VelocitySupply;
                    originalEdge.PipeSegment.VelocityReturn = edge.VelocityReturn;
                    originalEdge.PipeSegment.UtilizationRate = edge.UtilizationRate;
                }
            }

            _dataService.StoreCalculatedData(graphs.Select(g => g.Edges.Select(y => y.PipeSegment)));
        }
    }
}
