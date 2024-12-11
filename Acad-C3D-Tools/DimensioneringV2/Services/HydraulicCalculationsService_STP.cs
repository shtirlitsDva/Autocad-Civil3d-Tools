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
using DimensioneringV2.SteinerTreeProblem;
using System.Security.Policy;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        internal static void CalculateSTP(
            List<(
                Func<AnalysisFeature, dynamic> Getter, 
                Action<AnalysisFeature, dynamic> Setter)> props)
        {
            var graphs = _dataService.Graphs;

            //Reset the results
            foreach (var f in graphs.SelectMany(g => g.Edges.Select(e => e.PipeSegment))) f.ResetHydraulicResults();

            foreach (var graph in graphs)
            {
                // Find the root node
                var rootNode = graph.Vertices.FirstOrDefault(v => v.IsRootNode);
                if (rootNode == null)
                    throw new System.Exception("Root node not found.");

                var stpIn = graph.ToSTP();
                var result = stpIn.RunSTPSolver();
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
            }

            //_dataService.StoreCalculatedData(graphs.Select(g => g.Edges.Select(y => y.PipeSegment)));
        }
    }
}
