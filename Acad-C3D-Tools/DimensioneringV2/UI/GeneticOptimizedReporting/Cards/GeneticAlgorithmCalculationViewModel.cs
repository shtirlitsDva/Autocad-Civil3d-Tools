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
using System.Threading;
using GeneticSharp;
using OxyPlot.Axes;

namespace DimensioneringV2.UI.GeneticOptimizedReporting.Cards
{
    public partial class GeneticAlgorithmCalculationViewModel : GraphCalculationBaseViewModel
    {
        [ObservableProperty]
        private int bruteForceCount;

        [ObservableProperty]
        private int currentGeneration = 0;

        [ObservableProperty]
        private int generationsSinceLastUpdate = 0;

        [ObservableProperty]
        private bool stopRequested;

        private CancellationTokenSource _cancellationTokenSource;

        private const double threshold = -double.MaxValue + 100000;
        internal void ReportProgress(int generation, double fitness)
        {
            CurrentGeneration = generation;
            // Invalid fitness is double.MaxValue
            // Threshold is set to double.MaxValue - 100000
            if (fitness > threshold) Cost = fitness;
            
            AddPointToPlot(fitness, generation);
        }

        [ObservableProperty]
        private PlotModel plotModel;

        private readonly LineSeries _costSeries;

        public GeneticAlgorithmCalculationViewModel()
        {
            plotModel = new PlotModel { Title = null };// Title = "Cost Over Generations" };
            _costSeries = new LineSeries { Title = "Cost", MarkerType = MarkerType.Circle };
            plotModel.Series.Add(_costSeries);

            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = null,
                IsAxisVisible = false
            };
            plotModel.Axes.Add(yAxis);

            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void AddPointToPlot(double fitness, int generation)
        {
            if (_costSeries.Points.Count > 0)
            {
                var lastPoint = _costSeries.Points.Last();

                if (Math.Abs(lastPoint.Y - fitness) > 1e-9)
                {
                    _costSeries.Points.Add(new DataPoint(_costSeries.Points.Count, fitness));
                    Cost = fitness;
                    PlotModel.InvalidatePlot(true); // Refresh the plot
                    GenerationsSinceLastUpdate = 0;
                }
                else
                {
                    GenerationsSinceLastUpdate++;
                }
            }
            else
            {
                _costSeries.Points.Add(new DataPoint(0, fitness));
                Cost = fitness;
                PlotModel.InvalidatePlot(true); // Refresh the plot
                GenerationsSinceLastUpdate = 0;
            }

            CurrentGeneration = generation;
        }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        // Stop button command
        [RelayCommand]
        private void Stop()
        {
            _cancellationTokenSource.Cancel();
            StopRequested = true;
        }
    }
}
