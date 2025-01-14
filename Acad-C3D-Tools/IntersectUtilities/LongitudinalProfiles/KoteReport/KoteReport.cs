using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.QuikGraphClasses;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using QuikGraph;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Geometry;
using System.IO;

namespace IntersectUtilities.LongitudinalProfiles.KoteReport
{
    internal static class KoteReport
    {
        private static HashSet<AdjacencyGraph<KRNode, KREdge>>? _graphs;

        public static void BuildGraphs(GraphCollection ographs)
        {
            _graphs = GraphBuilder.BuildGraphs<KRNode, KREdge>(ographs);
        }

        public static void GenerateKoteReport(HashSet<Database> ldbs)
        {
            if (_graphs == null) { prdDbg("_graphs is null!"); return; }

            //avoid cycles
            var visited = new HashSet<KRNode>();

            //Find the MIDT profiles for pipelines
            var midtProfiles = new Dictionary<KRNode, (Database db, Oid pid)>();

            #region Map dbs to profiles
            foreach (var graph in _graphs)
            {
                var root = graph.Vertices.Where(x => x.Root).FirstOrDefault();
                if (root == null) { prdDbg("No root found!"); continue; }

                Queue<KRNode> queue = new Queue<KRNode>();
                queue.Enqueue(root);

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    visited.Add(node);

                    //Ensure traversal
                    var children = graph.OutEdges(node).Select(x => x.Target);
                    foreach (var child in children)
                    {
                        if (visited.Contains(child)) continue;
                        queue.Enqueue(child);
                    }

                    //Skip the profile finding part if the node is a NA
                    if (node.Value is PipelineV2Na) { midtProfiles.Add(node, default); continue; }

                    //Find the MIDT profile
                    Profile? midtProfile = null;
                    foreach (var ldb in ldbs)
                    {
                        using (Transaction ltx = ldb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                var als = ldb.HashSetOfType<Alignment>(ltx);
                                if (als.Any(x => x.Name == node.Value.Name))
                                {
                                    var al = als.First(x => x.Name == node.Value.Name);
                                    ObjectIdCollection pids = al.GetProfileIds();

                                    foreach (Oid pid in pids)
                                    {
                                        var p = pid.Go<Profile>(ltx);
                                        if (p.Name.EndsWith("MIDT"))
                                        {
                                            midtProfile = p;
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ltx.Abort();
                                prdDbg(ex);
                                throw;
                            }
                            ltx.Abort();
                        }
                    }

                    if (midtProfile != null) midtProfiles.Add(node, (midtProfile.Database, midtProfile.Id));
                    else midtProfiles.Add(node, default);
                }
            }
            #endregion

            //avoid cycles
            visited.Clear();

            #region Populate connections
            //Determine the elvations at connections
            foreach (var graph in _graphs)
            {
                var root = graph.Vertices.Where(x => x.Root).FirstOrDefault();
                if (root == null) { prdDbg("No root found!"); continue; }

                //Establish connections and ports
                Queue<KRNode> queue = new Queue<KRNode>();
                queue.Enqueue(root);

                Profile p = null;
                while (queue.Count > 0)
                {
                    var parent = queue.Dequeue();
                    visited.Add(parent);

                    var children = graph.OutEdges(parent).Select(x => x.Target);

                    foreach (var child in children)
                    {
                        //Determine the location of the connection
                        var pt = child.Value.GetConnectionLocationToParent(parent.Value, 0.01);

                        //Also find the edge to store information about port connections
                        graph.TryGetEdge(parent, child, out KREdge edge);

                        #region Parent node
                        //Process parent node
                        var pst = parent.Value.GetStationAtPoint(pt);

                        //Determine the elevation at the station on parent pipeline
                        double pelev = -99;

                        if (midtProfiles[parent] == default)
                        {
                            // No profile was found for this pipeline
                            ConnectionUnknown con = new ConnectionUnknown(ConnectionDirection.Out, pst, pelev);
                            parent.AddConnection(con);
                            edge.SourceCon = con;
                        }
                        else
                        {
                            var (pdb, pid) = midtProfiles[parent];
                            using (Transaction tx = pdb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    p = pid.Go<Profile>(tx);
                                    pelev = p.ElevationAt(pst);
                                }
                                catch (Exception ex)
                                {
                                    tx.Abort();
                                    prdDbg($"Error at: {parent.Value.Name}, ST: {pst}, P: {p?.Name} \n" + ex);
                                    throw;
                                }
                                tx.Abort();
                            }

                            ConnectionKnownElevation con = new ConnectionKnownElevation(ConnectionDirection.Out, pst, pelev);
                            parent.AddConnection(con);
                            edge.SourceCon = con;
                        }
                        #endregion

                        #region child node
                        //Process child node
                        var cst = child.Value.GetStationAtPoint(pt);

                        //Determine the elevation at the station on parent pipeline
                        double celev = -99;

                        if (midtProfiles[child] == default)
                        {
                            // No profile was found for this pipeline
                            ConnectionUnknown con = new ConnectionUnknown(ConnectionDirection.In, cst, celev);
                            child.AddConnection(con);
                            edge.TargetCon = con;
                        }
                        else
                        {
                            var (pdb, pid) = midtProfiles[child];
                            using (Transaction tx = pdb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    p = pid.Go<Profile>(tx);
                                    celev = p.ElevationAt(cst);
                                }
                                catch (Exception ex)
                                {
                                    tx.Abort();
                                    prdDbg($"Error at: {child.Value.Name}, ST: {cst}, P: {p?.Name}\n" + ex);
                                    throw;
                                }
                                tx.Abort();
                            }

                            ConnectionKnownElevation con = new ConnectionKnownElevation(ConnectionDirection.In, cst, celev);
                            child.AddConnection(con);
                            edge.TargetCon = con;
                        }
                        #endregion

                        //Ensure traversal
                        if (visited.Contains(child)) continue;
                        queue.Enqueue(child);
                    }
                }
            }
            #endregion

            //Sort connections by station
            //First sort all connections by station
            foreach (var graph in _graphs)
                foreach (var node in graph.Vertices)
                    node.Connections = node.Connections.OrderBy(x => x.Station).ToList();

            #region Generate Kote Report
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("digraph G {");
            sb.AppendLine($"splines=polyline");
            sb.AppendLine($"rankdir=LR");
            sb.AppendLine($"ranksep=1.35");
            sb.AppendLine("edge [dir=none style=bold]");

            int graphCount = 0;
            foreach (var graph in _graphs)
            {
                graphCount++;
                sb.AppendLine($"subgraph G_{graphCount} {{");
                sb.AppendLine("node [shape=record, fontname=\"monospace bold\"];");
                sb.AppendLine(NodesToDot(graph));
                sb.AppendLine(EdgesToDot(graph));
                sb.AppendLine("}");
            }

            sb.AppendLine("}");

            //Check or create directory
            if (!Directory.Exists(@"C:\Temp\"))
                Directory.CreateDirectory(@"C:\Temp\");

            //Write the collected graphs to one file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\MyKoteReport.dot"))
            {
                file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
            }

            //Start the dot engine to create the graph
            System.Diagnostics.Process cmd = new System.Diagnostics.Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
            cmd.StartInfo.Arguments = @"/c ""dot -Tpdf MyKoteReport.dot > MyKoteReport.pdf""";
            cmd.Start();
            #endregion
        }

        private static string NodesToDot(AdjacencyGraph<KRNode, KREdge> graph)
        {
            StringBuilder sb = new StringBuilder();

            //Then print nodes
            foreach (var node in graph.Vertices.OrderBy(x => x.Value.Name))
            {
                sb.AppendLine();
                sb.Append($"\"node{node.Value.Name}\" " +
                    $"[label=\"{{{node.Value.Name}}}");
                foreach (var con in node.Connections)
                {
                    sb.Append(con.ToLabel(node.Connections.IndexOf(con)));
                }
                sb.Append("\"];");
            }

            return sb.ToString();
        }

        private static string EdgesToDot(AdjacencyGraph<KRNode, KREdge> graph)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var edge in graph.Edges.OrderBy(x => x.Source.Value.Name).ThenBy(x => x.Target.Value.Name))
            {
                int sidx = edge.Source.Connections.IndexOf(edge.SourceCon);
                int tidx = edge.Target.Connections.IndexOf(edge.TargetCon);

                sb.AppendLine();
                sb.Append(
                    $"\"node{edge.Source.Value.Name}\":p{sidx.ToString("D3")}:e " +
                    $"-> " +
                    $"\"node{edge.Target.Value.Name}\":p{tidx.ToString("D3")}:w;");
            }

            return sb.ToString();
        }
    }
}