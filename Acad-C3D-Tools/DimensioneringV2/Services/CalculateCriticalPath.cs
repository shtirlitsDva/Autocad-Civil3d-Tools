using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.GraphModel;

using NorsynHydraulicCalc;

using QuikGraph;
using QuikGraph.Algorithms;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.SubGraphs
{
    internal class CriticalPathService
    {
        internal static void Calculate(
            UndirectedGraph<NodeJunction, EdgePipeSegment> graph)
        {
            var cGraph = graph.CopyToBFConditional(e => e.PipeSegment.NumberOfBuildingsSupplied > 0);
            foreach (var edge in cGraph.Edges) edge.YankAllResults();
            var rootNode = cGraph.Vertices.First(v => v.IsRootNode);
            var clientNodes = cGraph.Vertices.Where(v => v.IsBuildingNode);

            var tryGetPaths = cGraph.ShortestPathsDijkstra(edge => edge.Length, rootNode);

            //NetTopologySuite.Features.FeatureCollection fc = 
            //    new NetTopologySuite.Features.FeatureCollection(
            //        graph.Edges.Select(x => x.PipeSegment));
            //NetTopologySuite.IO.GeoJsonWriter writer = new NetTopologySuite.IO.GeoJsonWriter();
            //string json = writer.Write(fc);
            //using (var sw = new StreamWriter("C:\\Temp\\testfc.geojson"))
            //{
            //    sw.Write(json);
            //}

            //ExportGraphToDot.Export(graph);

            List<(
                BFNode client,
                BFEdge stik,
                IEnumerable<BFEdge> path)> paths = new();

            foreach (var client in clientNodes)
            {
                if (tryGetPaths(client, out IEnumerable<BFEdge>? path))
                {
                    var edges = cGraph.AdjacentEdges(client);
                    if (edges.Count() > 1)
                        throw new Exception(
                            $"Client node {client.Location} has more than one adjacent edge!");

                    paths.Add((client, edges.First(), path));
                }
            }

            foreach (var path in paths)
            {
                path.stik.PressureLossAtClient =
                    path.path.Sum(e =>
                    (e.PressureGradientReturn +
                    e.PressureGradientSupply) *
                    e.Length) / 100000 + 
                    HydraulicSettingsService.Instance.Settings
                    .MinDifferentialPressureOverHovedHaner;

                //Push the calculated values back to the original edge
                path.stik.OriginalEdge.PipeSegment
                    .PressureLossAtClient = path.stik.PressureLossAtClient;
            }

            var criticalPath = paths.MaxBy(p => p.stik.PressureLossAtClient);

            foreach (var edge in criticalPath.path)
            {
                edge.IsCriticalPath = true;
                edge.OriginalEdge.PipeSegment.IsCriticalPath = true;                
            }
        }
    }
}