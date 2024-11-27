using Dimensionering.DimensioneringV2.Geometry;
using Dimensionering.DimensioneringV2.GraphModelRoads;
using dbg = IntersectUtilities.UtilsCommon.Utils.DebugHelper;
using utils = IntersectUtilities.UtilsCommon.Utils;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Features;

namespace Dimensionering.DimensioneringV2.GraphFeatures
{
    internal class GraphTranslator
    {
        //public static List<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> TranslateGraph(Graph originalGraph)        
        public static List<List<FeatureNode>> TranslateGraph(Graph originalGraph)
        {
            List<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> translatedGraphs = new();
            List<List<FeatureNode>> allFeatures = new();

            HashSet<SegmentNode> visited = new();

            foreach (var subgraph in originalGraph.ConnectedComponents)
            {
                UndirectedGraph<FeatureNode, Edge<FeatureNode>> translatedGraph = new();
                translatedGraphs.Add(translatedGraph);
                List<FeatureNode> nodes = new List<FeatureNode>();

                SegmentNode root = subgraph.RootNode;

                Stack<SegmentNode> stack = new();
                stack.Push(root); // seed the stack with the root node

                List<SegmentNode> originalNodes = new();
                bool startNew = false;

                while (stack.Count > 0)
                {
                    SegmentNode node = stack.Pop();

                    if (visited.Contains(node)) continue;
                    visited.Add(node);

                    if (startNew)
                    {
                        originalNodes = new();
                        startNew = false;
                    }

                    int degree = node.Neighbors.Count;

                    switch (degree)
                    {
                        case 1:
                            {
                                originalNodes.Add(node);
                                stack.Push(node.Neighbors[0]);
                                continue;
                            }
                        case 2:
                            {
                                originalNodes.Add(node);
                                SegmentNode neighbor1 = node.Neighbors[0];
                                SegmentNode neighbor2 = node.Neighbors[1];
                                if (!visited.Contains(neighbor1)) stack.Push(neighbor1);
                                if (!visited.Contains(neighbor2)) stack.Push(neighbor2);
                                continue;
                            }
                        case >= 3:
                            {
                                originalNodes.Add(node);
                                foreach (SegmentNode neighbor in node.Neighbors)
                                {
                                    if (!visited.Contains(neighbor))
                                    {
                                        stack.Push(neighbor);
                                    }
                                }

                                var lines = originalNodes.Select(n => n.ToLineString());
                                var merger = new NetTopologySuite.Operation.Linemerge.LineMerger();
                                merger.Add(lines);
                                var merged = merger.GetMergedLineStrings();
                                if (merged.Count > 1)
                                {
                                    dbg.CreateDebugLine(
                                        originalNodes[0].StartPoint.To3d(), utils.ColorByName("red"));
                                    foreach (var item in originalNodes)
                                    {
                                        dbg.CreateDebugLine(
                                            item.EndPoint.To3d(), utils.ColorByName("red"));
                                    }
                                    utils.prdDbg("Merging returned multiple linestrings!");
                                    return null;
                                }

                                FeatureNode fn = new FeatureNode(
                                    merged.First(), new AttributesTable());
                                nodes.Add(fn);

                                startNew = true;
                                continue;
                            }
                        case 0:
                            {
                                dbg.CreateDebugLine(
                                    node.StartPoint.To3d(), utils.ColorByName("red"));
                                dbg.CreateDebugLine(
                                    node.EndPoint.To3d(), utils.ColorByName("red"));
                                utils.prdDbg("Node has no neighbours!\n" +
                                    $"{node.ToString()}");
                                return null;
                            }
                    }
                }

                allFeatures.Add(nodes);
            }

            return allFeatures;
        }
    }
}