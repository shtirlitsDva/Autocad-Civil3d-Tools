using Dimensionering.DimensioneringV2.GraphModelRoads;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphFeatures
{
    internal class GraphTranslator
    {
        public static List<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> TranslateGraph(Graph originalGraph)
        {
            List<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> translatedGraphs = new();

            HashSet<SegmentNode> visited = new();

            foreach (var subgraph in originalGraph.ConnectedComponents)
            {
                UndirectedGraph<FeatureNode, Edge<FeatureNode>> translatedGraph = new();
                translatedGraphs.Add(translatedGraph);

                SegmentNode root = subgraph.RootNode;

                Stack<SegmentNode> stack = new();
                stack.Push(root); // seed the stack with the root node

                FeatureNode current;
                List<SegmentNode> originalNodes;
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

                    
                }
            }
        }
    }
}
