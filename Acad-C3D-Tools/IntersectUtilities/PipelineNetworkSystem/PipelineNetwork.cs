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

namespace IntersectUtilities.PipelineNetworkSystem
{
    public class PipelineNetwork
    {
        private HashSet<IPipelineV2> pipelines;
        private GraphCollection pipelineGraphs;
        private PropertySetHelper psh;

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
            var builder = new GraphBuilder();
            pipelineGraphs = builder.BuildPipelineGraphs(pipelines);
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
            GraphWorker gw = new GraphWorker();
            gw.AutoReversePolylines(pipelineGraphs);
        }
        public void AutoCorrectLengths()
        {
            GraphWorker gw = new GraphWorker();
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
        public StringBuilder CreateSizeArraysAndPrint()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var pipeline in pipelines.OrderBy(x => x.Name))
            {
                prdDbg("Pipeline: " + pipeline.Name);
                sb.AppendLine("Alignment: " + pipeline.Name);
                pipeline.CreateSizeArray();
                prdDbg(pipeline.Sizes.ToString());
                sb.AppendLine(pipeline.Sizes.ToString());
                sb.AppendLine();
            }
            return sb;
        }
        internal void CreateWeldPoints()
        {
            GraphWorker gw = new GraphWorker();
            gw.CreateWeldPoints(pipelineGraphs);
        }
    }
    public interface INode
    {
        INode Parent { get; set; }
        List<INode> Children { get; }
        void AddChild(INode child);
        IEnumerable<T> GetChildrenOfType<T>() where T : INode;
        string Name { get; }
        string Label { get; }
        string EdgesToDot();
        string NodesToDot();
    }
    public abstract class NodeBase : INode
    {
        public INode Parent { get; set; }
        public List<INode> Children { get; private set; }
        public virtual string Name { get; protected set; }
        public virtual string Label { get; protected set; }
        protected NodeBase()
        {
            Children = new List<INode>();
        }
        public void AddChild(INode child)
        {
            child.Parent = this;
            Children.Add(child);
        }
        public IEnumerable<T> GetChildrenOfType<T>() where T : INode => Children.OfType<T>();
        public string EdgesToDot()
        {
            var edges = new StringBuilder();
            GatherEdges(this, edges);
            return edges.ToString();
        }
        private void GatherEdges(INode node, StringBuilder edges)
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
        private void GatherNodes(INode node, StringBuilder nodes)
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
    public class PipelineNode : NodeBase
    {
        public IPipelineV2 Value { get; private set; }
        public PipelineNode(IPipelineV2 value) : base()
        {
            Value = value;
            Name = value.Name;
            Label = value.Label;
        }
    }
    public class Graph
    {
        public INode Root { get; private set; }
        public Graph(INode root)
        {
            Root = root;
        }
        internal string EdgesToDot() => Root.EdgesToDot();
        internal string NodesToDot() => Root.NodesToDot();
    }
    public class GraphCollection : List<Graph>
    {
        public GraphCollection(IEnumerable<Graph> graphs) : base(graphs)
        {

        }
    }
    public class GraphBuilder
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

                        var startSize = source.Sizes.Sizes.First();
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

                        var endSize = source.Sizes.Sizes.Last();
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
    public class GraphWorker
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
        internal void CreateWeldPoints(GraphCollection graphs)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            //////////////////////////////////////
            string blockLayerName = "0-SVEJSEPKT";
            string blockName = "SVEJSEPUNKT-NOTXT";
            string textLayerName = "Nonplot";
            double tolerance = 0.003;
            //////////////////////////////////////

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
                    allEnts.UnionWith(currentPipeline.Entities);
                }
            }
            #endregion

            prdDbg($"Number of entities participating: {allEnts.Count}");

            PipeSettingsCollection psc = PipeSettingsCollection.Load();

            DataTable dt = CsvData.FK;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
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

                    //List to gather ALL weld points
                    var wps = new List<WeldPointData2>();

                    //Gathers all weld points
                    foreach (Entity ent in allEnts)
                    {
                        switch (ent)
                        {
                            case Polyline pline:
                                {
                                    double pipeStdLength = psc.GetSettingsLength(pline);
                                    double pipeLength = pline.Length;
                                    double division = pipeLength / pipeStdLength;
                                    int nrOfSections = (int)division;
                                    double remainder = division - nrOfSections;

                                    //Get start point and intermediate points
                                    for (int j = 0; j < nrOfSections + 1; j++)
                                    {
                                        Point3d wPt = pline.GetPointAtDist(j * pipeStdLength);

                                        var wp = new WeldPointData2(
                                                wPt,
                                                psh.Pipeline.ReadPropertyString(ent, psh.PipelineDef.BelongsToAlignment),
                                                ent,
                                                GetPipeDN(ent), GetPipeType(ent), GetPipeSystem(ent));

                                        if (j != 0) wp.IsPolylineWeld = true;

                                        wps.Add(wp);
                                    }
                                    double modulo = pipeLength % pipeStdLength;

                                    if (modulo > tolerance && (pipeStdLength - modulo) > tolerance)
                                        //Add end point
                                        wps.Add(
                                            new WeldPointData2(
                                                pline.EndPoint,
                                                psh.Pipeline.ReadPropertyString(ent, psh.PipelineDef.BelongsToAlignment),
                                                ent,
                                                GetPipeDN(ent), GetPipeType(ent), GetPipeSystem(ent))
                                            );
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
                                                                result.Pipe,
                                                                DN,
                                                                GetPipeType(result.Pipe),
                                                                GetPipeSystem(result.Pipe)
                                                            ));
                                                        }

                                                    }
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
                                        string alignment = allPipelines.MinBy(x => x.GetDistanceToPoint(wPt)).Name;
                                        #endregion

                                        wps.Add(new WeldPointData2(
                                            wPt,
                                            alignment,
                                            br,
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

                    #region Place weldblocks
                    var ordered = wps.OrderBy(x => x.WeldPoint.X).ThenBy(x => x.WeldPoint.Y);
                    var clusters
                            = ordered.GroupByCluster((x, y) => GetDistance(x, y), 0.005)
                            .Where(x => x.Count() > 1 || (x.Count() == 1 && x.First().IsPolylineWeld)) //<-- KEEP AN EYE ON THIS!!! HAHA, GOTCHA!
                            .ToHashSet();
                    double GetDistance(WeldPointData2 first, WeldPointData2 second) =>
                        first.WeldPoint.DistanceHorizontalTo(second.WeldPoint);

                    //Prepare modelspace
                    BlockTableRecord modelSpace = localDb.GetModelspaceForWrite();
                    modelSpace.CheckOrOpenForWrite();
                    //Prepare block table record
                    if (!bt.Has(blockName)) throw new System.Exception("Block for weld points is missing!");
                    Oid btrId = bt[blockName];
                    BlockTableRecord btrWp = btrId.Go<BlockTableRecord>(tx);
                    List<AttributeDefinition> attDefs = new List<AttributeDefinition>();
                    foreach (Oid arOid in btrWp)
                    {
                        if (!arOid.IsDerivedFrom<AttributeDefinition>()) continue;
                        AttributeDefinition at = arOid.Go<AttributeDefinition>(tx);
                        if (!at.Constant) attDefs.Add(at);
                    }

                    prdDbg($"Detected {clusters.Count} welds!");
                    prdDbg("Placing welds...");
                    System.Windows.Forms.Application.DoEvents();

                    var pm = new Autodesk.AutoCAD.Runtime.ProgressMeter();
                    pm.Start("Placing welds...");
                    pm.SetLimit(clusters.Count);
                    int idx = 0;
                    foreach (var cluster in clusters)
                    {
                        idx++;
                        wr(idx.ToString());
                        pm.MeterProgress();

                        Point3d wpt = cluster.First().WeldPoint;
                        string alignment = cluster.FirstOrDefault(
                            x => x.AlignmentName.IsNotNoE()).AlignmentName;
                        int DN = cluster.First().DN;

                        var pipeline = allPipelines.Where(x => x.Name == alignment).FirstOrDefault();
                        if (pipeline == null)
                        {
                            prdDbg($"ERROR: Pipeline name {alignment} not found for weld point at {wpt}!");
                            continue;
                        }
                        var deriv = pipeline.GetFirstDerivative(wpt);
                        double rotation = Math.Atan2(deriv.Y, deriv.X);

                        var wpBr = new BlockReference(wpt, btrId);
                        wpBr.Rotation = rotation;
                        wpBr.Layer = blockLayerName;

                        wpBr.AddEntityToDbModelSpace(localDb);

                        foreach (var attDef in attDefs)
                        {
                            var attRef = new AttributeReference();
                            attRef.SetAttributeFromBlock(attDef, wpBr.BlockTransform);
                            attRef.Position = attDef.Position.TransformBy(wpBr.BlockTransform);
                            attRef.TextString = attDef.getTextWithFieldCodes();
                            attRef.Layer = textLayerName;
                            wpBr.AttributeCollection.AppendAttribute(attRef);
                            tx.AddNewlyCreatedDBObject(attRef, true);
                        }

                        psh.Pipeline.WritePropertyString(
                            wpBr, psh.PipelineDef.BelongsToAlignment, alignment);

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

                        Utils.SetDynBlockPropertyObject(wpBr, "System", (object)pipeType.PipeType.ToString());
                        Utils.SetDynBlockPropertyObject(wpBr, "PIPESIZE", (double)DN);
                        Utils.SetDynBlockPropertyObject(wpBr, "SYSNAVN", pipeSystem.PipeSystem.ToString());

                        try
                        {
                            wpBr.AttSync();
                        }
                        catch (System.Exception)
                        {
                            prdDbg("ERROR: " + wpBr.Position);
                            idx++;
                            continue;
                        }

                    }
                    pm.Stop();
                    prdDbg("Finished placing welds!");
                    #endregion
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
    }
}
