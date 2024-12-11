using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.SteinerTreeProblem;

using Mapsui.Providers.Wfs.Utilities;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2
{
    internal static class Extensions
    {
        public static Point2D To2D(this Point3d pt)
        {
            return new Point2D(pt.X, pt.Y);
        }

        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                                .Cast<DescriptionAttribute>()
                                .FirstOrDefault();
            return attribute?.Description ?? value.ToString();
        }

        public static STP ToSTP(this UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
        {
            var stp = new STP();

            #region Renumber all nodes
            //Number all nodes
            var node = graph.Vertices.Where(x => x.IsRootNode).FirstOrDefault();
            //If no root node is found, the first node is selected
            if (node == null) node = graph.Vertices.First();

            HashSet<NodeJunction> visited = new HashSet<NodeJunction>();
            Stack<NodeJunction> stack = new Stack<NodeJunction>();
            stack.Push(node);

            int nodeNumber = 0;
            while (stack.Count > 0)
            {
                node = stack.Pop();
                if (visited.Contains(node)) continue;
                visited.Add(node);
                nodeNumber++;
                node.STP_Node = nodeNumber;

                foreach (var v in graph.AdjacentVertices(node))
                {
                    stack.Push(v);
                }
            }
            #endregion

            //Add all nodes and terminals
            foreach (var v in graph.Vertices)
            {
                stp.AddNode(v);
                if (graph.AdjacentEdges(v).Count() == 1 &&
                    (int)graph.AdjacentEdges(v).First().PipeSegment.SegmentType == 1)
                {
                    stp.AddTerminal(v.STP_Node);
                }
            }
            //Add all edges
            foreach (var e in graph.Edges)
            {
                stp.AddEdge(e.Source.STP_Node, e.Target.STP_Node, (int)e.PipeSegment.Length);
            }

            return stp;
        }
    }
}
