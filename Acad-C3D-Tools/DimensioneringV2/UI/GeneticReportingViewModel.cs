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

namespace DimensioneringV2.UI
{
    internal class GeneticReportingViewModel : ObservableObject
    {
        private readonly BackgroundWorker _worker;
        private bool _isRunning;
        private double _latestFitness;
        private ObservableCollection<int> _fitnessValues;

        public GeneticReportingViewModel()
        {
            StopCommand = new RelayCommand(StopAlgorithm);
            _fitnessValues = new ObservableCollection<int>();

            PlotModel = new PlotModel { Title = "GA Optimization Progress" };

            // Initialize LineSeries
            var lineSeries = new LineSeries
            {
                Title = "Fitness Progress",
                MarkerType = MarkerType.Circle
            };
            PlotModel.Series.Add(lineSeries);

            _worker = new BackgroundWorker { WorkerSupportsCancellation = true };
            _worker.DoWork += RunAlgorithm;
        }
        public PlotModel PlotModel { get; }
        public ObservableCollection<ISeries> seriesCollection { get; private set; }
        public IRelayCommand StopCommand { get; }
        public event Action<GraphChromosome?, UndirectedGraph<BFNode, BFEdge>>? AnalysisCompleted;

        private void StopAlgorithm()
        {
            _isRunning = false;
            _worker.CancelAsync();
        }

        public void StartAlgorithm(GeneticAlgorithm ga, UndirectedGraph<BFNode, BFEdge> graph)
        {
            _isRunning = true;
            _worker.RunWorkerAsync((ga, graph));
        }

        private void RunAlgorithm(object? sender, DoWorkEventArgs e)
        {
            if (e.Argument is not (GeneticAlgorithm ga, UndirectedGraph<BFNode, BFEdge> graph)) return;

            if (seriesCollection == null)
            {
                seriesCollection = new ObservableCollection<ISeries>
                {
                    new LineSeries<int>()
                    {
                        Values = _fitnessValues,
                        Fill = null
                    }
                };
            }

            ga.GenerationRan += (s, args) =>
            {
                if (!_isRunning)
                {
                    e.Cancel = true;
                    ga.Stop();
                    return;
                }

                var bestChromosome = ga.BestChromosome;
                var bestFitness = -(int)(bestChromosome.Fitness ?? 0);
                if (Math.Abs(bestFitness - _latestFitness) > double.Epsilon)
                {
                    _latestFitness = bestFitness;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _fitnessValues.Add(bestFitness);

                        // Clear and update the series to reflect new values
                        seriesCollection.Clear();
                        seriesCollection.Add(new LineSeries<int>
                        {
                            Values = _fitnessValues,
                            Fill = null
                        });
                    });
                }
            };

            ga.Start();

            var bestChromosomeResult = ga.BestChromosome as GraphChromosome;
            Application.Current.Dispatcher.Invoke(() => AnalysisCompleted?.Invoke(bestChromosomeResult, graph));
        }
    }
}