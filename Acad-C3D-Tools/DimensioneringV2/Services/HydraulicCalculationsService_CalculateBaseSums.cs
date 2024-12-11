using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.ShortestPath;

using DimensioneringV2.GraphFeatures;
using System.Diagnostics;

using utils = IntersectUtilities.UtilsCommon.Utils;
using System.IO;
using NorsynHydraulicCalc;

namespace DimensioneringV2.Services
{
    internal partial class HydraulicCalculationsService
    {
        private static List<dynamic> CalculateBaseSums(
        UndirectedGraph<NodeJunction, EdgePipeSegment> graph,
        NodeJunction node, HashSet<NodeJunction> visited,
        List<(Func<AnalysisFeature, dynamic> Getter, Action<AnalysisFeature, dynamic> Setter)> props)
        {
            if (visited.Contains(node)) return props.Select(_ => (dynamic)0).ToList();

            visited.Add(node);

            List<dynamic> totalSums = props.Select(_ => (dynamic)0).ToList();

            // Traverse downstream nodes recursively
            foreach (var edge in graph.AdjacentEdges(node))
            {
                var neighbor = edge.GetOtherVertex(node);
                var downstreamSums = CalculateBaseSums(graph, neighbor, visited, props);
                for (int i = 0; i < props.Count; i++)
                {
                    totalSums[i] += downstreamSums[i];
                }
                for (int i = 0; i < props.Count; i++)
                {
                    var (getter, setter) = props[i];
                    setter(edge.PipeSegment, downstreamSums[i]);
                }

                //totalBuildings += buildingsFromNeighbor;
                //edge.PipeSegment.NumberOfBuildingsSupplied = buildingsFromNeighbor;
            }

            //If this is a leaf node, set the number of buildings supplied to the connected value

            if (totalSums.All(sum => sum == 0) && graph.AdjacentEdges(node).Count() == 1)
            {
                for (int i = 0; i < props.Count; i++)
                {
                    var (getter, setter) = props[i];
                    var value = getter(graph.AdjacentEdges(node).First().PipeSegment);
                    totalSums[i] = value;
                    setter(graph.AdjacentEdges(node).First().PipeSegment, value);
                }
            }

            return totalSums;
        }
    }
}
