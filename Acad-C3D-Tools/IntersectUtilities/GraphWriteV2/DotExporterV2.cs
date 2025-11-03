using IntersectUtilities.UtilsCommon.Graphs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IntersectUtilities.GraphWriteV2
{
    internal static class DotExporterV2
    {
        public static void Export(
            IList<ComponentTree> components,
            Func<NodeContext, string?> clusterSelector,
            Func<NodeContext, string?> nodeAttrSelector,
            Func<ComponentTree, string, string?> clusterAttrsSelector,
            Func<ComponentTree, NodeContext, NodeContext, string?> edgeAttrSelector,
            string dotPath,
            bool includeHeader = true)
        {
            var sbAll = new StringBuilder();
            if (includeHeader) sbAll.AppendLine("digraph G {");

            int idx = 0;
            foreach (var comp in components)
            {
                idx++;
                sbAll.AppendLine($"subgraph G_{idx} {{");
                sbAll.AppendLine("node [shape=record];");

                // Nodes (with clusters and attributes)
                sbAll.Append(comp.Graph.NodesToDot(
                    clusterSelector,
                    nodeAttrSelector,
                    key => clusterAttrsSelector(comp, key)));

                // Edges (with attributes)
                string EdgeAttrsSelector(Node<NodeContext> a, Node<NodeContext> b)
                {
                    return edgeAttrSelector(comp, a.Value, b.Value) ?? string.Empty;
                }
                sbAll.Append(comp.Graph.EdgesToDot(EdgeAttrsSelector));

                sbAll.AppendLine("}");
            }

            if (includeHeader) sbAll.AppendLine("}");

            var dir = Path.GetDirectoryName(dotPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
            File.WriteAllText(dotPath, sbAll.ToString());
        }
    }
}