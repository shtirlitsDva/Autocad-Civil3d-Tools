using DimensioneringV2.Services;
using DimensioneringV2.Services.GDALClient;
using DimensioneringV2.UI;

using DimensioneringV2.UI.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class TestElevations
    {
        internal async Task Execute()
        {
            try
            {
                var graphs = DataService.Instance.Graphs;

                var caches = graphs
                    .SelectMany(g => g.Edges.Select(e => e.PipeSegment))
                    .Select(f => f.Elevations)
                    .ToArray();

                var progress = new Progress<(int done, int total)>(t =>
                {
                    Utils.prtDbg($"Elevation sampling progress: {t.done}/{t.total}");
                });

                var dispatcher = new ElevationDispatcher();

                var res = await dispatcher.SampleAsync(caches, 5, progress: progress);

                if (res.Ok)
                {
                    AcContext.Current.Post(_ =>
                    {
                        AutoCAD.WriteElevations2CurrentDrawing.Write(caches);
                    }, null);
                }
                else Utils.prtDbg(res.Error);
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during elevations testing: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
    }
}
