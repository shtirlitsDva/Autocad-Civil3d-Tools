using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI;

using DimensioneringV2.UI.Infrastructure;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.MapCommands
{
    internal class Write2Dwg
    {
        internal void Execute(List<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs)
        {
            try
            {
                IEnumerable<AnalysisFeature> features =
                    graphs.SelectMany(x => x.Edges.Select(e => e.PipeSegment));

                AcContext.Current.Post(_ =>
                {
                    AutoCAD.Write2Dwg.Write(features);
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
