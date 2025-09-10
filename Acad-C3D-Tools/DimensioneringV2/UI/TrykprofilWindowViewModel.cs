using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.BruteForceOptimization;
using DimensioneringV2.Models.Trykprofil;

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DimensioneringV2.UI
{
    internal class TrykprofilWindowViewModel : ObservableObject
    {
        internal Dispatcher? Dispatcher { get; set; }

        public PlotModel? PressurePlot { get; private set; }
        public TrykprofilWindowViewModel() { }
        public void LoadData(IEnumerable<PressureProfileEntry> profile)
        {
            PressurePlot = new PlotModel { Title = "Trykniveau" };

            PressurePlot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Distance [m]"
            });
            PressurePlot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Trykniveau [mVS]"
            });

            var elevationSeries = new LineSeries
            {
                Title = "Kote",
                Color = OxyColor.Parse("#00FF0000")
            };
            var supplySeries = new LineSeries 
            {
                Title = "Frem",
                Color = OxyColor.Parse("#FFFF0000")
            };
            var returnSeries = new LineSeries 
            { 
                Title = "Retur",
                Color = OxyColor.Parse("#FF0000FF")
            };

            foreach (var p in profile)
            {
                elevationSeries.Points.Add(new DataPoint(p.Length, p.Elevation));
                supplySeries.Points.Add(new DataPoint(p.Length, p.SPmVS));
                returnSeries.Points.Add(new DataPoint(p.Length, p.RPmVS));
            }

            PressurePlot.Series.Add(elevationSeries);
            PressurePlot.Series.Add(supplySeries);
            PressurePlot.Series.Add(returnSeries);
            PressurePlot.InvalidatePlot(true);
        }
    }
}
