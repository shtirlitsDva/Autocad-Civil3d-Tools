using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dreambuild.AutoCAD;

using IntersectUtilities.UtilsCommon;
using GroupByCluster;
using static IntersectUtilities.UtilsCommon.Utils;

using DataTable = System.Data.DataTable;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using System.Diagnostics;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.UtilsCommon.Enums;
using System.Collections;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public class PipelineNetwork
    {
        private HashSet<IPipelineV2> pipelines;
        private GraphCollection pipelineGraphs;
        private PropertySetHelper psh;
        public GraphCollection PipelineGraphs => pipelineGraphs;

        public void CreatePipelineNetwork(IEnumerable<Entity> ents, IEnumerable<Alignment> als)
        {
            pipelines = new HashSet<IPipelineV2>();

            if (psh == null)
                psh = new PropertySetHelper(ents?.FirstOrDefault()?.Database);

            // Get all the names of the pipelines
            // because we need to create a network for each of them
            // we also need to get the names of parts that do not belong to any pipeline
            // ie. NA XX parts
            var pplNames = ents.Select(
                e => psh.Pipeline.ReadPropertyString(
                    e, psh.PipelineDef.BelongsToAlignment)).Distinct();

            // Create a network to be able to analyze our piping system
            foreach (var pplName in pplNames)
            {
                // Get all the parts that belong to the pipeline
                var pplEnts = ents.Where(
                    e => psh.Pipeline.ReadPropertyString(
                        e, psh.PipelineDef.BelongsToAlignment) == pplName);

                // Get the alignment that the pipeline belongs to
                var al = als.FirstOrDefault(a => a.Name == pplName);

                //prdDbg($"{pplEnts.Where(x => x is Polyline).Count()} - {pplEnts.Where(x => x is BlockReference).Count()}");

                // Create a pipeline network
                pipelines.Add(PipelineV2Factory.Create(pplEnts, al));
            }
        }
        public void CreatePipelineGraph()
        {
            var builder = new PipelineGraphBuilder();
            pipelineGraphs = builder.BuildPipelineGraphs(pipelines);
        }
        public IPipelineV2 GetPipeline(string name)
        {
            return pipelines.FirstOrDefault(x => x.Name == name);
        }
        public void PrintPipelineGraphs()
        {
            foreach (var graph in pipelineGraphs)
            {
                PrintNode(graph.Root, 0);
            }
        }
        public void PipelineGraphsToDot()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("digraph G {");

            int graphCount = 0;
            foreach (var graph in pipelineGraphs)
            {
                graphCount++;
                sb.AppendLine($"subgraph G_{graphCount} {{");
                sb.AppendLine("node [shape=record];");
                sb.AppendLine(graph.EdgesToDot());
                sb.AppendLine(graph.NodesToDot());
                sb.AppendLine("}");
            }

            sb.AppendLine("}");

            //Check or create directory
            if (!Directory.Exists(@"C:\Temp\"))
                Directory.CreateDirectory(@"C:\Temp\");

            //Write the collected graphs to one file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\MyTPN.dot"))
            {
                file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
            }

            //Start the dot engine to create the graph
            System.Diagnostics.Process cmd = new System.Diagnostics.Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
            cmd.StartInfo.Arguments = @"/c ""dot -Tpdf MyTPN.dot > MyTPN.pdf""";
            cmd.Start();
        }
        public void AutoReversePolylines()
        {
            PipelineGraphWorker gw = new PipelineGraphWorker();
            gw.AutoReversePolylines(pipelineGraphs);
        }
        public void AutoCorrectLengths()
        {
            PipelineGraphWorker gw = new PipelineGraphWorker();
            gw.CorrectPipesToCutLengths(pipelineGraphs);
        }
        private void PrintNode(INode node, int depth)
        {
            prdDbg(new string(' ', depth * 2) + node.Name); // Indent based on depth

            foreach (var child in node.Children)
            {
                PrintNode(child, depth + 1);
            }
        }
        public void CreateSizeArrays()
        {
            foreach (var pipeline in pipelines)
            {
                pipeline.CreateSizeArray();
            }
        }
        public List<(string Name, IPipelineSizeArrayV2 SizeArray)> GetAllSizeArrays(bool includeNas = true)
        {
            List<(string, IPipelineSizeArrayV2)> data = new();
            foreach (var pipeline in pipelines)
            {
                if (!includeNas && pipeline is PipelineV2Na) continue;
                data.Add((pipeline.Name, pipeline.PipelineSizes));
            }
            return data;
        }
        public StringBuilder PrintSizeArrays()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var pipeline in pipelines.OrderBy(x => x.Name))
            {
                prdDbg("Pipeline: " + pipeline.Name);
                sb.AppendLine("Alignment: " + pipeline.Name);
                prdDbg(pipeline.PipelineSizes.ToString());
                sb.AppendLine(pipeline.PipelineSizes.ToString());
                sb.AppendLine();
            }
            return sb;
        }
        internal void GatherWeldPoints(List<WeldPointData2> wps)
        {
            PipelineGraphWorker gw = new PipelineGraphWorker();
            gw.CreateWeldPoints(pipelineGraphs, wps);
        }
        internal void CreateWeldBlocks(List<WeldPointData2> wps)
        {
            //Create weld blocks
            PipelineGraphWorker gw = new PipelineGraphWorker();
            gw.CreateWeldBlocks(wps);
        }
    }
    //public interface INode<T>
    //{
    //    INode<T>? Parent { get; set; }
    //    IReadOnlyList<INode<T>> Children { get; }
    //    void AddChild(INode<T> child);
        
    //    T Value { get; }
    //    string Name { get; }
    //    string Label { get; }
    //    //string EdgesToDot();
    //    //string NodesToDot();
    //}
    public abstract class Node
    {
        public Node? Parent { get; set; }
        
        private readonly List<Node> _children = new();
        public IReadOnlyList<Node> Children => _children.AsReadOnly();
        public string Name { get; protected set; }
        public string Label { get; protected set; }
        
        protected Node(string name, string label)
        {
            Name = name; Label = label;
        }
        public void AddChild(Node child)
        {
            if (child is null) throw new ArgumentNullException(nameof(child));
            if (ReferenceEquals(child, this)) throw new InvalidOperationException("A node cannot be its own child.");
            if (child.Parent != null)
                child.Parent._children.Remove(child);

            child.Parent = this;
            _children.Add(child);
        }        
        public string EdgesToDot()
        {
            var edges = new StringBuilder();
            GatherEdges(this, edges);
            return edges.ToString();
        }
        private void GatherEdges(Node node, StringBuilder edges)
        {
            foreach (var child in node.Children)
            {
                edges.AppendLine($"\"{node.Name}\" -> \"{child.Name}\"");
                GatherEdges(child, edges);  // Recursive call to gather edges of children
            }
        }
        public string NodesToDot()
        {
            var nodes = new StringBuilder();
            GatherNodes(this, nodes);
            return nodes.ToString();
        }
        private void GatherNodes(Node node, StringBuilder nodes)
        {
            string color = "";
            if (node.Parent == null) color = " color = red";
            nodes.AppendLine($"\"{node.Name}\" [label={node.Label}{color}]");
            foreach (var child in node.Children)
            {
                GatherNodes(child, nodes);  // Recursive call to gather nodes of children
            }
        }
    }
    public class PipelineNode : Node
    {
        public override IPipelineV2 Value { get; }
        public PipelineNode(IPipelineV2 value) : base()
        {
            Value = value;
            Name = value.Name;
            Label = value.Label;
        }
    }
    public class Graph<T> : IReadOnlyCollection<INode<T>>
    {
        public INode<T> Root { get; private set; }

        public int Count => Dfs().Count();

        public Graph(INode root)
        {
            Root = root;
        }
        internal string EdgesToDot() => Root.EdgesToDot();
        internal string NodesToDot() => Root.NodesToDot();
        /// <summary>
        /// Breadth-first traversal starting at Root (or an optional start node).
        /// </summary>
        public IEnumerable<INode> Bfs(INode? start = null)
        {
            var root = start ?? Root;
            if (root is null) yield break;

            var q = new Queue<INode>();
            var seen = new HashSet<INode>();

            q.Enqueue(root);
            seen.Add(root);

            while (q.Count > 0)
            {
                var n = q.Dequeue();
                yield return n;

                foreach (var c in n.Children)
                {
                    if (c is not null && seen.Add(c))
                        q.Enqueue(c);
                }
            }
        }

        /// <summary>
        /// Depth-first (pre-order) traversal starting at Root (or an optional start node).
        /// </summary>
        public IEnumerable<INode> Dfs(INode? start = null)
        {
            var root = start ?? Root;
            if (root is null) yield break;

            var st = new Stack<INode>();
            var seen = new HashSet<INode>();

            st.Push(root);
            seen.Add(root);

            while (st.Count > 0)
            {
                var n = st.Pop();
                yield return n;

                // Push right-to-left so the leftmost child is visited first.
                for (int i = n.Children.Count - 1; i >= 0; i--)
                {
                    var c = n.Children[i];
                    if (c is not null && seen.Add(c))
                        st.Push(c);
                }
            }
        }

        public IEnumerator<INode> GetEnumerator()
        {
            return Dfs().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    public class GraphCollection : List<Graph>
    {
        public GraphCollection(IEnumerable<Graph> graphs) : base(graphs)
        {

        }
    }
    public class PipelineGraphBuilder
    {
        public GraphCollection BuildPipelineGraphs(IEnumerable<IPipelineV2> pipelines)
        {
            GraphCollection graphs = new GraphCollection(new List<Graph>());

            var groups = pipelines.GroupConnected((x, y) => x.IsConnectedTo(y, 0.05));
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var maxDNQuery = group.MaxByEnumerable(x => x.GetMaxDN());

                IPipelineV2 entryPipeline;
                if (maxDNQuery.Count() > 1)
                {//Multiple candidates for MAXDN found

                    #region Case 1
                    // Case 1.)
                    // Two alignments with same max DN
                    // But one of them is connected at both ends
                    // This is not the entry pipeline
                    // The other is only connected on one end

                    entryPipeline = maxDNQuery.Where(x => !AreBothEndsConnected(x, group, x.GetMaxDN())).FirstOrDefault();
                    if (entryPipeline == default) entryPipeline = maxDNQuery.First();

                    bool AreBothEndsConnected(IPipelineV2 source, IEnumerable<IPipelineV2> other, int endDn)
                    {
                        source.CreateSizeArray();

                        var startSize = source.PipelineSizes.Sizes.First();
                        bool startConnected = true;
                        if (startSize.DN == endDn)
                        {
                            Point3d sp = source.StartPoint;
                            startConnected = other.Where(x => x.Name != source.Name).Any(x =>
                            {
                                Point3d testP = x.GetClosestPointTo(sp, false);
                                return testP.DistanceHorizontalTo(sp) < 0.05;
                            });
                        }

                        var endSize = source.PipelineSizes.Sizes.Last();
                        bool endConnected = true;
                        if (endSize.DN == endDn)
                        {
                            Point3d ep = source.EndPoint;
                            endConnected = other.Where(x => x.Name != source.Name).Any(x =>
                            {
                                Point3d testP = x.GetClosestPointTo(ep, false);
                                return testP.DistanceHorizontalTo(ep) < 0.05;
                            });
                        }

                        return startConnected && endConnected;
                    }
                    #endregion
                }
                else entryPipeline = maxDNQuery.First();

                group.Remove(entryPipeline);
                var root = new PipelineNode(entryPipeline);
                Graph graph = new Graph(root);
                graphs.Add(graph);

                var stack = new Stack<PipelineNode>();
                stack.Push(root);

                while (stack.Count > 0)
                {
                    var node = stack.Pop();
                    var children = group.Where(x => x.IsConnectedTo(node.Value, 0.05)).ToList();
                    foreach (var child in children)
                    {
                        var childNode = new PipelineNode(child);
                        node.AddChild(childNode);
                        stack.Push(childNode);
                        group.Remove(child);
                    }
                }
            }
            return graphs;
        }
    }
    public class PipelineGraphWorker
    {
        public void AutoReversePolylines(GraphCollection graphs)
        {
            foreach (var graph in graphs)
            {
                var root = graph.Root;
                prdDbg("Root node: " + ((PipelineNode)root).Value.Name);
                if (!(root is PipelineNode)) throw new System.Exception("PipelineNodes expected!");
                var stack = new Stack<PipelineNode>();
                stack.Push(root as PipelineNode);

                while (stack.Count > 0)
                {
                    PipelineNode currentNode = stack.Pop();
                    foreach (var child in currentNode.Children)
                    {
                        if (!(child is PipelineNode)) throw new System.Exception("PipelineNodes expected!");
                        stack.Push(child as PipelineNode);
                    }
                    IPipelineV2 currentPipeline = currentNode.Value;

                    // General case
                    if (currentNode.Parent != null)
                    {
                        PipelineNode parentNode = currentNode.Parent as PipelineNode;
                        if (parentNode == null) throw new Exception("PipelineNodes expected!");
                        Point3d connectionLocation = currentPipeline.GetConnectionLocationToParent(
                            parentNode.Value, 0.05);
                        currentPipeline.AutoReversePolylines(connectionLocation);
                    }
                    else // Root case
                    {
                        Point3d connectionLocation = Point3d.Origin;
                        if (currentNode.Children.Count == 0)
                        {
                            connectionLocation = currentPipeline.GetLocationForMaxDN();
                            currentPipeline.AutoReversePolylines(connectionLocation);
                        }
                        else
                        {
                            if (currentNode.Children.Any(
                                x => currentPipeline.DetermineUnconnectedEndPoint(
                                    ((PipelineNode)x).Value, 0.05, out connectionLocation)))
                            {
                                currentPipeline.AutoReversePolylines(connectionLocation);
                            }
                            else
                            {
                                connectionLocation = currentPipeline.GetLocationForMaxDN();
                                currentPipeline.AutoReversePolylines(connectionLocation);
                            }
                        }
                    }
                }
            }
        }
        public Result CorrectPipesToCutLengths(GraphCollection graphs)
        {
            Result result = new Result();
            foreach (var graph in graphs)
            {
                var root = graph.Root;
                prdDbg("Root node: " + ((PipelineNode)root).Value.Name);
                if (!(root is PipelineNode)) throw new Exception("PipelineNodes expected!");
                var stack = new Stack<PipelineNode>();
                stack.Push(root as PipelineNode);

                while (stack.Count > 0)
                {
                    PipelineNode currentNode = stack.Pop();
                    foreach (var child in currentNode.Children)
                    {
                        if (!(child is PipelineNode)) throw new Exception("PipelineNodes expected!");
                        stack.Push(child as PipelineNode);
                    }
                    IPipelineV2 currentPipeline = currentNode.Value;

                    Point3d connectionLocation = Point3d.Origin;
                    // General case
                    if (currentNode.Parent != null)
                    {
                        PipelineNode parentNode = currentNode.Parent as PipelineNode;
                        if (parentNode == null) throw new Exception("PipelineNodes expected!");
                        connectionLocation = currentPipeline
                            .GetConnectionLocationToParent(parentNode.Value, 0.05);
                    }
                    else // Root case
                    {
                        if (currentNode.Children.Count == 0)
                            connectionLocation = currentPipeline.GetLocationForMaxDN();
                        else
                        {
                            if (currentNode.Children.Any(
                                x => currentPipeline.DetermineUnconnectedEndPoint(
                                    ((PipelineNode)x).Value, 0.05, out connectionLocation)))
                            { }
                            else
                            {
                                connectionLocation = currentPipeline.GetLocationForMaxDN();
                            }
                        }
                    }

                    Result r = currentPipeline.CorrectPipesToCutLengths(connectionLocation);
                    result.Combine(r);
                }
            }

            prdDbg(result.ToString());
            return result;
        }
        private int charCount = 0;
        private int wrapLength = 100;
        private void wr(string msg)
        {
            msg = " " + msg;
            charCount += msg.Length;
            Application.DocumentManager.CurrentDocument.Editor.WriteMessage(msg);
            System.Windows.Forms.Application.DoEvents();
            if (charCount > wrapLength)
            {
                prdDbg();
                charCount = 0;
            }
        }
        private void wr(string msg, Stopwatch sw)
        {
            msg = " " + msg;
            charCount += msg.Length;
            Application.DocumentManager.CurrentDocument.Editor.WriteMessage(msg);
            System.Windows.Forms.Application.DoEvents();
            if (charCount > wrapLength)
            {
                Application.DocumentManager.CurrentDocument.Editor.WriteMessage(" " + sw.Elapsed.ToString());
                prdDbg();
                charCount = 0;
            }
        }

        //////////////////////////////////////
        private string blockLayerName = "0-SVEJSEPKT";
        //private string blockName = "SVEJSEPUNKT-NOTXT";
        private string blockName = "SVEJSEPUNKT-V2";
        private string textLayerName = "Nonplot";
        private double tolerance = 0.005;
        //////////////////////////////////////

        internal void CreateWeldPoints(GraphCollection graphs, List<WeldPointData2> wps)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PipeSettingsCollection psc = PipeSettingsCollection.LoadWithValidation(localDb);
            DataTable dt = CsvData.FK;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetHelper psh = new PropertySetHelper(localDb);

                    HashSet<Entity> allEnts = new HashSet<Entity>();
                    HashSet<IPipelineV2> allPipelines = new HashSet<IPipelineV2>();

                    #region Get all participating entities
                    foreach (Graph graph in graphs)
                    {
                        var root = graph.Root;
                        prdDbg("Root node: " + ((PipelineNode)root).Value.Name);
                        if (!(root is PipelineNode)) throw new Exception("PipelineNodes expected!");
                        var stack = new Stack<PipelineNode>();
                        stack.Push(root as PipelineNode);

                        while (stack.Count > 0)
                        {
                            PipelineNode currentNode = stack.Pop();
                            foreach (var child in currentNode.Children)
                            {
                                if (!(child is PipelineNode)) throw new Exception("PipelineNodes expected!");
                                stack.Push(child as PipelineNode);
                            }
                            IPipelineV2 currentPipeline = currentNode.Value;
                            allPipelines.Add(currentPipeline);
                            allEnts.UnionWith(currentPipeline.PipelineEntities);
                        }
                    }
                    #endregion

                    prdDbg($"Number of entities participating: {allEnts.Count}");

                    #region Delete previous blocks
                    //Delete previous blocks
                    var existingBlocks = localDb.GetBlockReferenceByName(blockName);
                    foreach (BlockReference br in existingBlocks)
                    {
                        br.CheckOrOpenForWrite();
                        br.Erase(true);
                    }
                    #endregion

                    #region Create layer for weld blocks and
                    localDb.CheckOrCreateLayer(blockLayerName);
                    localDb.CheckOrCreateLayer(textLayerName);
                    #endregion

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    #region Import weld block if missing
                    //All of this should be moved to a helper class
                    if (!bt.Has(blockName))
                    {
                        prdDbg("Block for weld annotation is missing!\n" +
                            "IMPORT MANUALLY!!!");
                        //prdDbg("Block for weld annotation is missing! Importing...");
                        //Database blockDb = new Database(false, true);
                        //blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                        //    FileOpenMode.OpenForReadAndAllShare, false, null);
                        //Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        //Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        //Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                        //BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        //ObjectIdCollection idsToClone = new ObjectIdCollection();
                        //idsToClone.Add(sourceBt[blockName]);

                        //IdMapping mapping = new IdMapping();
                        //blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        //blockTx.Commit();
                        //blockTx.Dispose();
                        //blockDb.Dispose();
                    }
                    else
                    {
                        //Check if existing block is latest version
                        var btr = localDb.GetBlockTableRecordByName(blockName);

                        #region Read present block version
                        string version = "";
                        foreach (Oid oid in btr)
                        {
                            if (oid.IsDerivedFrom<AttributeDefinition>())
                            {
                                var atdef = oid.Go<AttributeDefinition>(tx);
                                if (atdef.Tag == "VERSION") { version = atdef.TextString; break; }
                            }
                        }
                        if (version.IsNoE()) version = "1";
                        else if (version.Contains("v")) version = version.Replace("v", "");
                        int blockVersion = Convert.ToInt32(version);
                        #endregion

                        #region Determine latest version
                        var query = dt.AsEnumerable()
                                .Where(x => x["Navn"].ToString() == blockName)
                                .Select(x => x["Version"].ToString())
                                .Select(x => { if (x == "") return "1"; else return x; })
                                .Select(x => Convert.ToInt32(x.Replace("v", "")))
                                .OrderBy(x => x);

                        if (query.Count() == 0)
                            throw new System.Exception($"Block {blockName} is not present in FJV Dynamiske Komponenter.csv!");
                        int maxVersion = query.Max();
                        #endregion

                        if (maxVersion != blockVersion)
                            throw new System.Exception(
                                $"Block {blockName} v{blockVersion} is not latest version v{maxVersion}! " +
                                $"Update with latest version from:\n" +
                                $"X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg\n" +
                                $"WARNING! This can break existing blocks! Caution is advised!");
                    }
                    #endregion

                    //Gathers all weld points
                    foreach (Entity ent in allEnts)
                    {
                        string alignment = psh.Pipeline.ReadPropertyString(ent, psh.PipelineDef.BelongsToAlignment);
                        var pipeline = allPipelines.Where(x => x.Name == alignment).FirstOrDefault();
                        if (pipeline == null)
                        {
                            prdDbg($"ERROR: Pipeline name {alignment} not found for entity {ent.Handle}!");
                            continue;
                        }

                        switch (ent)
                        {
                            case Polyline pline:
                                {
                                    double pipeStdLength = psc.GetSettingsLength(pline);
                                    double pipeLength = pline.Length;
                                    double division = pipeLength / pipeStdLength;
                                    int nrOfSections = (int)division;
                                    double remainder = division - nrOfSections;

                                    //If the length is just a little bit too short, we add one more section
                                    if ((pipeStdLength - remainder * pipeStdLength) < tolerance) nrOfSections++;

                                    //The logic is as follows:
                                    //First create using for loop
                                    //It will create weld points at start and intermediate points
                                    //EXCEPT for the last point of intermediate points
                                    //Thus we have j < nrOfSections (note no +1 here now)
                                    //Then add the end point of pipe

                                    //at last determine if the remainder is within tolerance
                                    //if it is, no last intermedate point
                                    //if not, the last intermediate point is added

                                    //Get start point and intermediate points
                                    for (int j = 0; j < nrOfSections; j++)
                                    {
                                        Point3d wPt = pline.GetPointAtDist(j * pipeStdLength);
                                        var deriv = pipeline.GetFirstDerivative(wPt);

                                        var wp = new WeldPointData2(
                                                wPt,
                                                psh.Pipeline.ReadPropertyString(ent, psh.PipelineDef.BelongsToAlignment),
                                                ent.Id,
                                                Math.Atan2(deriv.Y, deriv.X),
                                                GetPipeDN(ent), GetPipeType(ent), GetPipeSystem(ent));

                                        if (j != 0) wp.IsPolylineWeld = true;

                                        wps.Add(wp);
                                    }

                                    //Add end point
                                    {
                                        var deriv = pipeline.GetFirstDerivative(pline.EndPoint);

                                        //add end pointS
                                        wps.Add(
                                            new WeldPointData2(
                                                pline.EndPoint,
                                                psh.Pipeline.ReadPropertyString(ent, psh.PipelineDef.BelongsToAlignment),
                                                ent.Id,
                                                Math.Atan2(deriv.Y, deriv.X),
                                                GetPipeDN(ent), GetPipeType(ent), GetPipeSystem(ent))
                                            );
                                    }

                                    //NOW determine if we need the last intermediate point
                                    if ((pipeLength - pipeStdLength * nrOfSections) > tolerance)
                                    {
                                        Point3d wPt = pline.GetPointAtDist(pipeStdLength * nrOfSections);
                                        var deriv = pipeline.GetFirstDerivative(wPt);

                                        var wp = new WeldPointData2(
                                                wPt,
                                                psh.Pipeline.ReadPropertyString(ent, psh.PipelineDef.BelongsToAlignment),
                                                ent.Id,
                                                Math.Atan2(deriv.Y, deriv.X),
                                                GetPipeDN(ent), GetPipeType(ent), GetPipeSystem(ent));

                                        wp.IsPolylineWeld = true;

                                        wps.Add(wp);
                                    }
                                }
                                break;
                            case BlockReference br:
                                {
                                    var type = br.GetPipelineType();

                                    BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                                    foreach (Oid oid in btr)
                                    {
                                        if (!oid.IsDerivedFrom<BlockReference>()) continue;
                                        BlockReference nestedBr = oid.Go<BlockReference>(tx);
                                        if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                                        Point3d wPt = nestedBr.Position;
                                        wPt = wPt.TransformBy(br.BlockTransform);

                                        var deriv = pipeline.GetFirstDerivative(wPt);
                                        var rotation = Math.Atan2(deriv.Y, deriv.X);

                                        #region Read DN
                                        int DN = 0;
                                        bool parseSuccess = false;
                                        if (nestedBr.Name.Contains("BRANCH"))
                                        {
                                            parseSuccess = int.TryParse(
                                                br.ReadDynamicCsvProperty(DynamicProperty.DN2), out DN);
                                        }
                                        else //Else catches "MAIN" and ordinary case
                                        {
                                            parseSuccess = int.TryParse(
                                                br.ReadDynamicCsvProperty(DynamicProperty.DN1), out DN);

                                            //For following types, we need to read the point on the
                                            //pipe that we are branching from
                                            switch (type)
                                            {
                                                case PipelineElementType.Afgreningsstuds:
                                                case PipelineElementType.Svanehals:
                                                    {
                                                        var result = allPipelines
                                                            .SelectMany(p => p.GetPolylines())
                                                            .Select(p => new
                                                            {
                                                                Distance = p.GetClosestPointTo(wPt, false).DistanceHorizontalTo(wPt),
                                                                Location = p.GetClosestPointTo(wPt, false),
                                                                Pipe = p
                                                            })
                                                            .MinBy(p => p.Distance);

                                                        if (result != null && result.Distance < 0.001)
                                                        {
                                                            wps.Add(new WeldPointData2(
                                                                result.Location,
                                                                psh.Pipeline.ReadPropertyString(
                                                                    result.Pipe, psh.PipelineDef.BelongsToAlignment),
                                                                result.Pipe.Id,
                                                                rotation,
                                                                DN,
                                                                GetPipeType(result.Pipe),
                                                                GetPipeSystem(result.Pipe)
                                                            ));
                                                        }

                                                    }
                                                    break;
                                                case PipelineElementType.Reduktion:
                                                    var size = pipeline.PipelineSizes.GetSizeAtStation(
                                                        pipeline.GetStationAtPoint(wPt));
                                                    DN = size.DN;
                                                    break;
                                            }
                                        }

                                        if (!parseSuccess)
                                        {
                                            prdDbg($"ERROR: Parsing of DN failed for block handle: {br.Handle}! " +
                                                $"Returned value: {br.ReadDynamicCsvProperty(DynamicProperty.DN1)} and" +
                                                $" {br.ReadDynamicCsvProperty(DynamicProperty.DN2)}");
                                        }
                                        #endregion

                                        #region Determine correct alignment name
                                        string al = allPipelines.MinBy(x => x.GetDistanceToPoint(wPt)).Name;
                                        #endregion

                                        wps.Add(new WeldPointData2(
                                            wPt,
                                            al,
                                            br.Id,
                                            rotation,
                                            DN,
                                            ComponentSchedule.GetPipeTypeEnum(br),
                                            ComponentSchedule.GetPipeSystemEnum(br)
                                        ));
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    throw;
                }
                tx.Commit();
            }
        }

        internal void CreateWeldBlocks(List<WeldPointData2> wps)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            double tolerance = 0.005;

            #region Cluster welds
            var ordered = wps.OrderBy(x => x.WeldPoint.X).ThenBy(x => x.WeldPoint.Y);
            var clusters
                    = ordered.GroupByCluster(GetDistance, tolerance)
                    .Where(x => x.Count() > 1 || (x.Count() == 1 && x.First().IsPolylineWeld)) //<-- KEEP AN EYE ON THIS!!! HAHA, GOTCHA!
                    .ToList();
            double GetDistance(WeldPointData2 first, WeldPointData2 second) =>
                first.WeldPoint.DistanceHorizontalTo(second.WeldPoint);
            #endregion

#if DEBUG
            List<double> timings = new List<double>();
            Stopwatch sw = new Stopwatch();
#endif
            Stopwatch linesw = new Stopwatch();

            prdDbg($"Detected {clusters.Count} welds!");
            prdDbg("Placing welds...");
            System.Windows.Forms.Application.DoEvents();

            var pm = new Autodesk.AutoCAD.Runtime.ProgressMeter();
            pm.Start("Placing welds...");
            pm.SetLimit(clusters.Count);
            int idx = 0;

            linesw.Start();

            foreach (var chunk in clusters.Chunk(25))
            {
                using (Transaction tTx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        PropertySetHelper pshT = new PropertySetHelper(localDb);

                        #region Prepare block table record and attributes
                        Oid modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                        BlockTableRecord modelSpace = modelspaceId.Go<BlockTableRecord>(tTx, OpenMode.ForWrite);

                        //Prepare block table record
                        BlockTable bt = localDb.BlockTableId.Go<BlockTable>(tTx);
                        if (!bt.Has(blockName)) throw new System.Exception("Block for weld points is missing!");
                        Oid btrId = bt[blockName];
                        BlockTableRecord btrWp = btrId.Go<BlockTableRecord>(tTx);
                        List<AttributeDefinition> attDefs = new List<AttributeDefinition>();
                        foreach (Oid arOid in btrWp)
                        {
                            if (!arOid.IsDerivedFrom<AttributeDefinition>()) continue;
                            AttributeDefinition at = arOid.Go<AttributeDefinition>(tTx);
                            if (!at.Constant) attDefs.Add(at);
                        }
                        #endregion

                        foreach (var cluster in chunk)
                        {
#if DEBUG
                            sw.Restart();
#endif
                            idx++;
                            wr(idx.ToString(), linesw);
                            pm.MeterProgress();

                            Point3d wpt = cluster.First().WeldPoint;
                            string? alignment = cluster.FirstOrDefault(
                                x => x.AlignmentName.IsNotNoE())?.AlignmentName;
                            int DN = cluster.First().DN;

                            using (var wpBr = new BlockReference(wpt, btrId))
                            {
                                wpBr.Rotation = cluster.First().Rotation;
                                wpBr.Layer = blockLayerName;

                                Oid id = modelSpace.AppendEntity(wpBr);
                                tTx.AddNewlyCreatedDBObject(wpBr, true);

                                foreach (var attDef in attDefs)
                                {
                                    using (var attRef = new AttributeReference())
                                    {
                                        attRef.SetAttributeFromBlock(attDef, wpBr.BlockTransform);
                                        attRef.Position = attDef.Position.TransformBy(wpBr.BlockTransform);
                                        attRef.TextString = attDef.getTextWithFieldCodes();
                                        attRef.Layer = textLayerName;
                                        wpBr.AttributeCollection.AppendAttribute(attRef);
                                        tTx.AddNewlyCreatedDBObject(attRef, true);
                                    }
                                }

                                pshT.Pipeline.WritePropertyString(
                                    wpBr, pshT.PipelineDef.BelongsToAlignment, alignment);

                                var pipeSystem = cluster.FirstOrDefault(
                                    x => x.PipeSystem != PipeSystemEnum.Ukendt);
                                if (pipeSystem == null)
                                {
                                    prdDbg($"ERROR: Pipe system not found for weld point at {wpt}!");
                                    continue;
                                }

                                var pipeType = cluster.FirstOrDefault(
                                    x => x.PipeType != PipeTypeEnum.Ukendt);
                                if (pipeType == null)
                                {
                                    prdDbg($"ERROR: Pipe type not found for weld point at {wpt}!");
                                    continue;
                                }

                                wpBr.SetAttributeStringValue("SYSTEM", pipeType.PipeType.ToString());
                                wpBr.SetAttributeStringValue("PIPESIZE", DN.ToString());
                                wpBr.SetAttributeStringValue("SYSNAVN", pipeSystem.PipeSystem.ToString());

                                //Utils.SetDynBlockPropertyObject(wpBr, "SYSTEM", (object)pipeType.PipeType.ToString());
                                //Utils.SetDynBlockPropertyObject(wpBr, "PIPESIZE", (double)DN);
                                //Utils.SetDynBlockPropertyObject(wpBr, "SYSNAVN", pipeSystem.PipeSystem.ToString());

                                //This erases data in ordinary attributes
                                //But is needed for dynamic block properties
                                //try
                                //{
                                //    wpBr.AttSync();
                                //}
                                //catch (System.Exception)
                                //{
                                //    prdDbg("ERROR: " + wpBr.Position);
                                //    idx++;
                                //    continue;
                                //}
                            }
#if DEBUG
                            sw.Stop();
                            timings.Add(sw.Elapsed.TotalMicroseconds);
#endif
                        }
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg(ex);
                        tTx.Abort();
                        pm.Stop();
                        pm.Dispose();
                        throw;
                    }
                    tTx.Commit();
                }
            }

            pm.Stop();
            prdDbg("Finished placing welds!");

            linesw.Stop();
            prdDbg($"Total time to place welds: {linesw.Elapsed}");
#if DEBUG
            File.WriteAllLines(@"C:\Temp\WeldPointTimings.txt", timings.Select(x => x.ToString()));
#endif
        }
    }
}