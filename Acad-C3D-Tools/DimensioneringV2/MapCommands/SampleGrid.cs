using DimensioneringV2.Common;
using DimensioneringV2.Services;
using DimensioneringV2.Services.GDALClient;
using DimensioneringV2.UI;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
                    Utils.prtDbg($"Grid sampling progress: {t.done}/{t.total}");
                });

                var dispatcher = new SampleGridDispatcher();

                var list = new List<(double X, double Y, double E)>();

                var res = await dispatcher.SampleGridAsync(list, 10, progress: progress);

                if (res.Ok)
                {
                    // Load settings (project id)
                    var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    var settings = SettingsSerializer<ElevationSettings>.Load(doc);
                    if (string.IsNullOrWhiteSpace(settings?.BaseFileName))
                    {
                        Utils.prtDbg("Terræn data ikke sat op for denne tegning!");
                        return;
                    }

                    string projectId = settings!.BaseFileName!;
                    // Resolve base path from drawing filename
                    var dbFile = doc.Database.Filename;
                    if (string.IsNullOrWhiteSpace(dbFile))
                    {
                        Utils.prtDbg("Kan ikke finde DWG-filen på disk.");
                        return;
                    }

                    string basePath = Path.GetDirectoryName(dbFile)!;
                    var dir = Path.Combine(basePath, "Elevations");

                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var outFile = Path.Combine(dir, $"{projectId}_ElevationGrid_10m.csv");

                    using (var sw = new StreamWriter(outFile, false, Encoding.UTF8))
                    {
                        sw.WriteLine("X;Y;E");
                        foreach (var p in list.OrderBy(x => x.X).ThenBy(y => y.Y))
                        {
                            sw.WriteLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "{0:F6};{1:F6};{2:F6}",
                                p.X, p.Y, p.E));
                        }
                    }

                    Utils.prtDbg($"Grid points written to: \n {outFile}\n");
                }
                else Utils.prtDbg(res.Error);

                //if (res.Ok)
                //{
                //    AcContext.Current.Post(_ =>
                //    {
                //        AutoCAD.WriteGridPoints2CurrentDrawing.Write(list);
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
