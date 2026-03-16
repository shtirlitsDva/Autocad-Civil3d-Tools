using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI;

using DimensioneringV2.UI.Infrastructure;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DimensioneringV2.MapCommands
{
    internal class Dim2ImportDims
    {
        internal void Execute(List<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs)
        {
            try
            {
                var result = MessageBox.Show(
                    "Include service lines?",
                    "Confirm",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes &&
                    result != MessageBoxResult.No) return;

                bool includeServiceLines = result == MessageBoxResult.Yes;

                var edges = graphs.SelectMany(x => x.Edges.Select(e => e.PipeSegment));

                if (!includeServiceLines) edges = edges.Where(
                    x => x.SegmentType != NorsynHydraulicCalc.SegmentType.Stikledning);

                AcContext.Current.Post(_ =>
                {
                    AutoCAD.Dim2WriteDims.Write(edges);
                }, null);
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"An error occurred during Dim2ImportDims: {ex.Message}");
                Utils.prtDbg(ex);
            }
        }
    }
}
