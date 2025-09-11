using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class Write2Dwg
    {
        internal void Execute()
        {
            try
            {
                var graphs = DataService.Instance.Graphs;

                IEnumerable<AnalysisFeature> reprojected =
                    graphs.SelectMany(x => ProjectionService.ReProjectFeatures(
                        x.Edges.Select(x => x.PipeSegment), "EPSG:3857", "EPSG:25832"));

                AcContext.Current.Post(_ =>
                {
                    AutoCAD.Write2Dwg.Write(reprojected);
                }, null);
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during Write2Dwg: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
    }
}
