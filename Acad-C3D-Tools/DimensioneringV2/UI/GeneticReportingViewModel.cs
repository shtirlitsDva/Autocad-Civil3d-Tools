using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Genetic;

using GeneticSharp;

using QuikGraph;

using OxyPlot;
using OxyPlot.Series;
using System.Windows.Threading;
using System.Threading;

namespace DimensioneringV2.UI
{
    internal class GeneticReportingViewModel : ObservableObject
    {
        internal Dispatcher? Dispatcher { get; set; }
        private CancellationTokenSource _cancellationTokenSource;
        private int _generationCounter;
        private int _sinceLastUpdateCounter;
        private double _currentCost;
        
        public GeneticReportingViewModel()
        {
            StopCommand = new RelayCommand(StopAlgorithm);
            _cancellationTokenSource = new CancellationTokenSource();
            PlotModel = new PlotModel { Title = "GA Optimization Progress" };

            // Initialize LineSeries
            var lineSeries = new LineSeries
            {
                Title = "Fitness Progress",
                MarkerType = MarkerType.Circle
            };
            PlotModel.Series.Add(lineSeries);
        }
        public PlotModel PlotModel { get; }
        public IRelayCommand StopCommand { get; }
        private void StopAlgorithm()
        {
            _cancellationTokenSource.Cancel();
        }
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public int GenerationCounter
        {
            get => _generationCounter;
            set => SetProperty(ref _generationCounter, value);
        }
        public int SinceLastUpdateCounter
        {
            get => _sinceLastUpdateCounter;
            set => SetProperty(ref _sinceLastUpdateCounter, value);
        }
        public double CurrentCost
        {
            get => _currentCost;
            set => SetProperty(ref _currentCost, value);
        }
        private const double threshold = -double.MaxValue + 100000;
        public void UpdatePlot(int generation, double fitness)
        {
            var lineSeries = PlotModel.Series.FirstOrDefault() as LineSeries;
            if (lineSeries is null) throw new Exception("No lineseries found!");

            if (fitness > threshold) // Only add meaningful values
            {
                //Get the last point to compare values
                //Only add new points if the fitness is different from the last point
                if (lineSeries.Points.Count > 0)
                {
                    var lastPoint = lineSeries.Points.Last();

                    if (Math.Abs(lastPoint.Y - fitness) > 1e-9)
                    {
                        lineSeries.Points.Add(new DataPoint(lineSeries.Points.Count, fitness));
                        CurrentCost = fitness;
                        PlotModel.InvalidatePlot(true); // Refresh the plot
                        SinceLastUpdateCounter = 0;
                    }
                    else
                    {
                        SinceLastUpdateCounter++;
                    }
                }
                else
                {
                    lineSeries.Points.Add(new DataPoint(lineSeries.Points.Count, fitness));
                    CurrentCost = fitness;
                    SinceLastUpdateCounter = 0;
                    PlotModel.InvalidatePlot(true); // Refresh the plot
                }
            }

            GenerationCounter = generation; // Update generation counter
        }
    }
}