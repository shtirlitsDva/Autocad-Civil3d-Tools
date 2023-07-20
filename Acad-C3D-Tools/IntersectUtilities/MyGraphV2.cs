﻿using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities
{
    internal class GraphNodeV2
    {
        public Pipeline Node { get; set; }
        public List<GraphNodeV2> Children { get; set; }

        public GraphNodeV2(Pipeline node)
        {
            Node = node;
            Children = new List<GraphNodeV2>();
        }

        public static GraphNodeV2 CreateGraph(List<Pipeline> group, double tolerance)
        {
            // Find the pipeline with the largest pipe size
            Pipeline rootPipeline = group.OrderByDescending(pipeline => pipeline.MaxDn).First();

            // Create a dictionary to hold the graph nodes for each pipeline
            var nodes = group.ToDictionary(pipeline => pipeline, pipeline => new GraphNodeV2(pipeline));

            // Connect the graph nodes based on the IsConnected method of the pipelines
            foreach (var pipeline in group)
            {
                var node = nodes[pipeline];

                foreach (var otherPipeline in group)
                {
                    if (pipeline != otherPipeline && pipeline.IsConnectedTo(otherPipeline, tolerance))
                    {
                        nodes[otherPipeline].Children.Add(node);  // the other node is the child of this node
                    }
                }
            }

            // Return the root node of the graph
            return nodes[rootPipeline];
        }

        public static void ToDot(List<GraphNodeV2> rootNodes)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("digraph G {");

            int subGraphId = 1;
            foreach (GraphNodeV2 rootNode in rootNodes)
            {
                // Start a new subgraph for this group of pipelines
                sb.AppendLine($"  subgraph G_{subGraphId} {{");
                sb.AppendLine("    node [shape=record];");

                // Use a queue to perform a breadth-first traversal of this group
                Queue<GraphNodeV2> queue = new Queue<GraphNodeV2>();
                queue.Enqueue(rootNode);

                HashSet<GraphNodeV2> visited = new HashSet<GraphNodeV2>();
                visited.Add(rootNode);

                while (queue.Count > 0)
                {
                    GraphNodeV2 current = queue.Dequeue();
                    foreach (GraphNodeV2 child in current.Children)
                    {
                        if (!visited.Contains(child))
                        {
                            //sb.AppendLine($"    \"{current.Node.Alignment.Name}\" -> \"{child.Node.Alignment.Name}\";");
                            queue.Enqueue(child);
                            visited.Add(child);
                        }
                    }
                }

                // Here you can add attributes for the subgraph, like its label and color
                //sb.AppendLine($"    label = \"Cluster {subGraphId}\";");
                //sb.AppendLine("    color = red;");

                sb.AppendLine("  }");

                subGraphId++;
            }

            sb.AppendLine("}");

            //Check or create directory
            if (!Directory.Exists(@"C:\Temp\"))
                Directory.CreateDirectory(@"C:\Temp\");

            //Write the collected graphs to one file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\MyGraph.dot"))
            {
                file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
            }

            //Start the dot engine to create the graph
            System.Diagnostics.Process cmd = new System.Diagnostics.Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
            cmd.StartInfo.Arguments = @"/c ""dot -Tpdf MyGraph.dot > MyGraph.pdf""";
            cmd.Start();
        }

    }
}
