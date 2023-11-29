using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using psh = IntersectUtilities.PipelineNetworkSystem.PropertySetHelper;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public class PipelineNetwork
    {
        private HashSet<IPipelineV2> pipelines;
        private GraphCollection pipelineGraphs;

        public void CreatePipelineNetwork(IEnumerable<Entity> ents, IEnumerable<Alignment> als)
        {
            pipelines = new HashSet<IPipelineV2>();

            PropertySetHelper.Init(ents?.FirstOrDefault()?.Database);

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

        private void PrintNode(INode node, int depth)
        {
            prdDbg(new String(' ', depth * 2) + node.Name); // Indent based on depth

            foreach (var child in node.Children)
            {
                PrintNode(child, depth + 1);
            }
        }
    }
    public static class PropertySetHelper
    {
        public static PropertySetManager Graph;
        public static PropertySetManager Pipeline;
        public static PSetDefs.DriGraph GraphDef;
        public static PSetDefs.DriPipelineData PipelineDef;

        public static void Init(Database db)
        {
            if (db == null) throw new System.Exception(
                "Either ents collection, first element or its' database is null!");

            Graph = new PropertySetManager(db, PSetDefs.DefinedSets.DriGraph);
            GraphDef = new PSetDefs.DriGraph();
            Pipeline = new PropertySetManager(db, PSetDefs.DefinedSets.DriPipelineData);
            PipelineDef = new PSetDefs.DriPipelineData();
        }
    }
    public interface INode
    {
        INode Parent { get; set; }
        List<INode> Children { get; }
        void AddChild(INode child);
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
                edges.AppendLine($"{node.Name} -> {child.Name}");
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
            if (node.Parent == null) color = "color = red";
            nodes.AppendLine($"{node.Name} [label={node.Label}{color}]");
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
                var maxDN = group.MaxBy(x => x.GetMaxDN()).FirstOrDefault();
                group.Remove(maxDN);
                var root = new PipelineNode(maxDN);
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
}
