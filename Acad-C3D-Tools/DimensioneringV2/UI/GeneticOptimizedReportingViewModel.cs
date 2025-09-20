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
using DimensioneringV2.UI;

using GeneticSharp;

using QuikGraph;

using OxyPlot;
using OxyPlot.Series;
using System.Windows.Threading;
using System.Threading;

namespace DimensioneringV2.UI
{
    internal partial class GeneticOptimizedReportingViewModel : ObservableObject
    {
        internal Dispatcher? Dispatcher { get; set; }
        [ObservableProperty]
        private ObservableCollection<GraphCalculationBaseViewModel> graphCalculations = new();

        [ObservableProperty]
        private double totalCost;

        private DispatcherTimer _timer;

        public GeneticOptimizedReportingViewModel()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => RecalculateTotalCost();
            _timer.Start();
        }

        private void RecalculateTotalCost()
        {
            // Sum the 'Cost' across all child VMs
            double sum = 0;
            foreach (var calcVm in GraphCalculations)
            {
                sum += calcVm.Cost;
            }

            TotalCost = sum;
        }
    }
}