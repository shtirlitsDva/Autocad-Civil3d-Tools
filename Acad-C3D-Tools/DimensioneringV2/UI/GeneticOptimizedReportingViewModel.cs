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
    }
}