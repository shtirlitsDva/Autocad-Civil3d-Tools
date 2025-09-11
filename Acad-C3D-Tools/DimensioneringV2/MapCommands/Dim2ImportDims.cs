using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DimensioneringV2.MapCommands
{
    internal class Dim2ImportDims
    {
        internal void Execute()
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

                var graphs = DataService.Instance.Graphs;

                var edges = graphs.SelectMany(x => x.Edges.Select(x => x.PipeSegment));

                if (!includeServiceLines) edges = edges.Where(
                    x => x.SegmentType != NorsynHydraulicCalc.SegmentType.Stikledning);

                IEnumerable<AnalysisFeature> reprojected =
                    ProjectionService.ReProjectFeatures(edges, "EPSG:3857", "EPSG:25832");

                AcContext.Current.Post(_ =>
                {
                    AutoCAD.Dim2WriteDims.Write(reprojected);
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
