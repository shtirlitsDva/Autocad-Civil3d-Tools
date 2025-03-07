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

using static DimensioneringV2.UI.BruteForceProgressContext;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static void CalculateBFAnalysis(
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
                    VM.UpdateRound(optimizationCounter);

                    var bridges = FindBridges.DoFindThem(bfGraph);
                    //if (bridges.Any(notMst.Contains)) bridges.IntersectWith(notMst);
                    VM.UpdateBridges(bridges.Count);

                    if (bridges.Count == bfGraph.Edges.Count())
                    {
                        optimizationContinues = false;
                        break;
                    }

                    var list = bfGraph.Edges.Where(x => !bridges.Contains(x)).ToList();
                    list.Shuffle();
                    VM.UpdateRemovalCandidates(list.Count);

                    var results = new ConcurrentBag<(UndirectedGraph<BFNode, BFEdge> graph, double cost)>();

                    int counter = 0;
                    Parallel.ForEach(list, candidate =>
                    {
                        counter++;
                        VM.UpdateCurrentCandidate(counter);
                        var cGraph = bfGraph.CopyWithNewEdges();
                        var cCandidate = cGraph.Edges.First(
                            x => x.Source == candidate.Source &&
                            x.Target == candidate.Target);
                        cGraph.RemoveEdge(cCandidate);

                        double cost = CalculateBFCost(cGraph, props);
                        //double cost = cGraph.Edges.Sum(x => x.Length);
                        results.Add((cGraph, cost));
                    });

                    var bestResult = results.MinBy(x => x.cost);
                    bfGraph = bestResult.graph;
                }

                //Update the original graph with the results from the best result
                foreach (var edge in bfGraph.Edges)
                {
                    edge.PushAllResults();
                }
            }

            _dataService.CalculationsFinished(graphs.Select(g => g.Edges.Select(y => y.PipeSegment)));
        }
    }
}
