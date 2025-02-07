using BruTile.Wms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot.Series;
using OxyPlot;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.UI
{
    public partial class GeneticAlgorithmCalculationViewModel : GraphCalculationBaseViewModel
    {
        [ObservableProperty]
        private int bruteForceCount;

        [ObservableProperty]
        private int currentGeneration;

        [ObservableProperty]
        private int generationsSinceLastUpdate;

        [ObservableProperty]
        private double cost;

        [ObservableProperty]
        private bool stopRequested;

        // When cost changes, we update the chart automatically. 
        partial void OnCostChanged(double oldValue, double newValue)
        {
            AddPointToPlot(newValue, CurrentGeneration);
        }

        [ObservableProperty]
        private PlotModel plotModel;

        private readonly LineSeries _costSeries;

        public GeneticAlgorithmCalculationViewModel()
        {
            plotModel = new PlotModel { Title = "Cost Over Generations" };
            _costSeries = new LineSeries { Title = "Cost", MarkerType = MarkerType.None };
            plotModel.Series.Add(_costSeries);

            // Optionally add an initial point
            AddPointToPlot(Cost, CurrentGeneration);
        }

        private void AddPointToPlot(double newCost, int generation)
        {
            if (_costSeries.Points.Count == 0
                || Math.Abs(_costSeries.Points[^1].Y - newCost) > 1e-12)
            {
                _costSeries.Points.Add(new DataPoint(generation, newCost));
                plotModel.InvalidatePlot(true);
            }
        }

        // Stop button command
        [RelayCommand]
        private void Stop()
        {
            StopRequested = true;
        }
    }
}
