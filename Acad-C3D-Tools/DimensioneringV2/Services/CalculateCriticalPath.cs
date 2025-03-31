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
            var rootNode = graph.Vertices.First(v => v.IsRootNode);
            var clientNodes = graph.Vertices.Where(v => v.IsBuildingNode);

            var tryGetPaths = graph.ShortestPathsDijkstra(
                edge => edge.PipeSegment.Length, rootNode);

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
                NodeJunction client,
                EdgePipeSegment stik,
                IEnumerable<EdgePipeSegment> path)> paths = new();

            foreach (var client in clientNodes)
            {
                if (tryGetPaths(client, out IEnumerable<EdgePipeSegment>? path))
                {
                    var edges = graph.AdjacentEdges(client);
                    if (edges.Count() > 1)
                        throw new Exception(
                            $"Client node {client.Location} has more than one adjacent edge!");

                    paths.Add((client, edges.First(), path));
                }
            }

            foreach (var path in paths)
            {
                path.stik.PipeSegment.PressureLossAtClient =
                    path.path.Sum(e =>
                    (e.PipeSegment.PressureGradientReturn +
                    e.PipeSegment.PressureGradientSupply) *
                    e.PipeSegment.Length) / 100000 + 
                    HydraulicSettingsService.Instance.Settings
                    .MinDifferentialPressureOverHovedHaner;
            }

            var criticalPath = paths.MaxBy(p => p.stik.PipeSegment.PressureLossAtClient);

            foreach (var edge in criticalPath.path)
            {
                edge.PipeSegment.IsCriticalPath = true;
            }
        }
    }
}