using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Common;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models.Trykprofil;
using DimensioneringV2.Services;
using DimensioneringV2.Services.Elevations;
using DimensioneringV2.UI;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class Trykprofil
    {
        internal async Task Execute(AnalysisFeature? feature)
        {
            if (feature == null) return;
            if (feature.NumberOfBuildingsSupplied == 0) return;

            var settings = HydraulicSettingsService.Instance.Settings;
            List<PressureProfileEntry> entries = new();

            try
            {
                await Task.Run(async () =>
                {
                    var graphs = DataService.Instance.Graphs;

                    //Handle elevations
                    var caches = graphs
                        .SelectMany(g => g.Edges.Select(e => e.PipeSegment))
                        .Select(f => f.Elevations);

                    var res = OpResult<int>.Success(0);

                    if (!caches.All(x => x.Sampled))
                    {
                        var progress = new Progress<(int done, int total)>(t =>
                        {
                            Utils.prtDbg($"Elevation sampling progress: {t.done}/{t.total}");
                        });

                        var gdal = new ElevationDispatcher();
                        res = await gdal.SampleAsync(caches, 5, progress: progress);
                    }

                    if (!res.Ok)
                    {
                        Utils.prtDbg("Could not sample elevations: " + res.Error);
                        return;
                    }

                    //Get the graph that the selected feature is part of
                    var oGraph = graphs.Where(
                        g => g.Edges.Any(
                            e => e.PipeSegment == feature))
                    .FirstOrDefault();

                    if (oGraph == null) return; //<-- HERE MAKE THE WINDOW DISPLAY AN ERROR TEXT

                    UndirectedGraph<BFNode, BFEdge> graph = oGraph.CopyToBFConditional(
                        x => x.PipeSegment.NumberOfBuildingsSupplied > 0);
                    foreach (var edge in graph.Edges) edge.YankAllResults(); //Get results from the base features

                    var targetEdge = graph.Edges.Where(
                        x => x.OriginalEdge.PipeSegment == feature).FirstOrDefault();
                    if (targetEdge == null) return; //<-- HERE MAKE THE WINDOW DISPLAY AN ERROR TEXT

                    var targetNode = graph.AdjacentDegree(targetEdge.Source) == 1 ?
                        targetEdge.Source : targetEdge.Target;

                    var root = graph.GetRoot();
                    if (root == null) return; //<-- HERE MAKE THE WINDOW DISPLAY AN ERROR TEXT                    

                    //Calculate the new pressure profile with elevations
                    //Step 1. Calculate holdetryk
                    //Holdetryk er maxkote for netværket - kote fra forsyningspunktet
                    //Max kote:
                    caches = graph.Edges.Select(x => x.OriginalEdge.PipeSegment.Elevations);
                    var maxKote = caches.Max(x => x.GetDefaultProfile().Max(y => y.Elevation));
                    //Holdetryk
                    var holdeTrykMVS = maxKote // - graph.RootElevation(root)
                    + settings.TillægTilHoldetrykMVS;

                    //Required overpressure
                    var neededSupplyPmVS = graph.Edges.Max(
                        x => x.OriginalEdge.PipeSegment.PressureLossAtClient)
                    .mVS();

                    //Total pressure required at supply point
                    var maxPmVS = holdeTrykMVS + neededSupplyPmVS;

                    //Compute the elevation profile, this should be oriented in the path dir
                    var path = graph.OrientedProfiles(root, targetNode);

                    double paToBar = 100_000.0;

                    double length = 0, elevation = 0,
                    spmvs = maxPmVS, rpmvs = holdeTrykMVS,
                    spbar = 0, rpbar = 0;

                    foreach (var edge in path)
                    {
                        var (f, p) = edge;
                        for (int i = 0; i < p.Count; i++)
                        {
                            var pp = p[i];
                            var prevSt = i == 0 ? 0 : p[i - 1].Station;
                            var deltaL = pp.Station - prevSt;

                            //Trykniveau
                            length += deltaL;
                            elevation = pp.Elevation;
                            spmvs -= (deltaL * f.PressureGradientSupply / paToBar).mVS();
                            rpmvs += (deltaL * f.PressureGradientReturn / paToBar).mVS();

                            //Tryk
                            spbar = (spmvs - elevation).Bar();
                            rpbar = (rpmvs - elevation).Bar();

                            entries.Add(
                                new(length, elevation, spmvs, rpmvs, spbar, rpbar));
                        }
                    }
                });

                var trykprofilWindow = new TrykprofilWindow(entries);
                trykprofilWindow.Show();
                TrykprofilWindowContext.VM = (TrykprofilWindowViewModel)trykprofilWindow.DataContext;
                TrykprofilWindowContext.VM.Dispatcher = trykprofilWindow.Dispatcher;
            }
            catch (Exception ex)
            {
                Utils.prtDbg(ex);
                return;
            }
        }
    }
}
