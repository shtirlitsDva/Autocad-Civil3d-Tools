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
                //Try to use MST to achieve a better guidance for edge removal
                //MST does not influence the result, no effect whatsoever
                //var mst = bfGraph.MinimumSpanningTreePrim(e => e.Length).ToHashSet();
                //var notMst = bfGraph.Edges.Except(mst).ToHashSet();

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

                #region Old code
                //var stpOut = STP.ParseOutput(result);

                //var stpTree = new UndirectedGraph<NodeJunction, EdgePipeSegment>();
                //stpTree.AddVertexRange(graph.Vertices.Where(x => stpOut.HasNode(x.STP_Node)));
                //HashSet<STP_Edge> orphans = new HashSet<STP_Edge>();
                //foreach (var resEdge in stpOut.Edges)
                //{
                //    var edge = GetEdge(resEdge.Source, resEdge.Target);
                //    if (edge == null)
                //    {
                //        orphans.Add(resEdge);
                //        continue;
                //    }
                //    stpTree.AddEdge(edge);
                //}
                //if (orphans.Count > 0)
                //{
                //    Document doc = Application.DocumentManager.MdiActiveDocument;
                //    Database db = doc.Database;
                //    using (DocumentLock docLock = doc.LockDocument())
                //    using (Transaction tx = db.TransactionManager.StartTransaction())
                //    {
                //        db.CheckOrCreateLayer("_GraphOrphans", 1);

                //        foreach (var orphan in orphans)
                //        {
                //            var n1 = graph.Vertices.FirstOrDefault(x => x.STP_Node == orphan.Source.Name);
                //            var n2 = graph.Vertices.FirstOrDefault(x => x.STP_Node == orphan.Target.Name);
                //            if (n1 == null || n2 == null) 
                //            { utils.prdDbg($"Orphan couldn't even match nodes!"); continue; }

                //            Utils.DebugHelper.CreateDebugLine(
                //                n1.Location.To3d(), n2.Location.To3d(), utils.ColorByName("cyan"), "_GraphOrphans");
                //        }

                //        tx.Commit();
                //    }

                //    return;
                //}

                //EdgePipeSegment GetEdge(STP_Node source, STP_Node target)
                //{
                //    var s = graph.Vertices.FirstOrDefault(v => v.STP_Node == source.Name);
                //    var t = graph.Vertices.FirstOrDefault(v => v.STP_Node == target.Name);
                //    if (s == null || t == null) return null;

                //    var edge = graph.Edges.FirstOrDefault(e => e.Source == s && e.Target == t);
                //    if (edge != null) return edge;
                //    edge = graph.Edges.FirstOrDefault(e => e.Source == t && e.Target == s);
                //    return edge;
                //}

                ////// Traverse the network and calculate
                ////// the sums of all properties as given in the props list
                ////// These sums lays the foundation for the hydraulic calculations
                //var visited = new HashSet<NodeJunction>();
                //CalculateBaseSums(stpTree, rootNode, visited, props);

                //CalculateHydraulics(stpTree); 
                #endregion
            }

            _dataService.StoreCalculatedData(graphs.Select(g => g.Edges.Select(y => y.PipeSegment)));
        }
    }
}
