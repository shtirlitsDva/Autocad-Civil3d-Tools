using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

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
        private void PrintNode(INode node, int depth)
        {
            prdDbg(new String(' ', depth * 2) + node.Name); // Indent based on depth

            foreach (var child in node.Children)
            {
                PrintNode(child, depth + 1);
            }
        }
        public void CreateSizeArraysAndPrint()
        {
            foreach (var pipeline in pipelines.OrderBy(x => x.Name))
            {
                prdDbg("Pipeline: " + pipeline.Name);
                pipeline.CreateSizeArray();
                prdDbg(pipeline.Sizes.ToString());
            }
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
                var maxDNQuery = group.MaxBy(x => x.GetMaxDN());

                IPipelineV2 entryPipeline;
                if (maxDNQuery.Count() > 1)
                {//Multiple candidates for MAXDN found

                    #region Case 1
                    // Case 1.)
                    // Two alignments with same max DN
                    // But one of them is connected at both ends
                    // This is not the entry pipeline
                    // The other is only connected on one end

                    entryPipeline = maxDNQuery.Where(x => !AreBothEndsConnected(x, group, x.GetMaxDN())).First();

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
        public void CorrectPipesToCutLengths(GraphCollection graphs)
        {
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

                    // General case
                    if (currentNode.Parent != null)
                    {
                        PipelineNode parentNode = currentNode.Parent as PipelineNode;
                        if (parentNode == null) throw new Exception("PipelineNodes expected!");
                        Point3d connectionLocation = currentPipeline.GetConnectionLocationToParent(
                            parentNode.Value, 0.05);
                        
                        currentPipeline.CorrectPipesToCutLengths(connectionLocation);
                    }
                    else // Root case
                    {
                        Point3d connectionLocation = Point3d.Origin;
                        if (currentNode.Children.Count == 0) connectionLocation =
                                currentPipeline.GetLocationForMaxDN();
                        else
                        {
                            if (currentNode.Children.Any(
                                x => currentPipeline.DetermineUnconnectedEndPoint(
                                    ((PipelineNode)x).Value, 0.05, out connectionLocation)))
                            {
                                currentPipeline.CorrectPipesToCutLengths(connectionLocation);
                            }
                            else
                            {
                                connectionLocation = currentPipeline.GetLocationForMaxDN();
                                currentPipeline.CorrectPipesToCutLengths(connectionLocation);
                            }
                        }
                    }
                }
            }
        }
    }
}
