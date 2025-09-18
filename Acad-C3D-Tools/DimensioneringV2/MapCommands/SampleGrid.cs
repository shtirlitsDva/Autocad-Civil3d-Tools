using DimensioneringV2.Services;
using DimensioneringV2.Services.GDALClient;
using DimensioneringV2.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class SampleGrid
    {
        internal async Task Execute()
        {
            try
            {
                var progress = new Progress<(int done, int total)>(t =>
                {
                    Utils.prtDbg($"Elevation sampling progress: {t.done}/{t.total}");
                });

                var dispatcher = new SampleGridDispatcher();

                //var res = await dispatcher.SampleAsync(caches, 5, progress: progress);

                //if (res.Ok)
                //{
                //    AcContext.Current.Post(_ =>
                //    {
                //        AutoCAD.WriteElevations2CurrentDrawing.Write(caches);
                //    }, null);
                //}
                //else Utils.prtDbg(res.Error);
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during elevations testing: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
    }
}
