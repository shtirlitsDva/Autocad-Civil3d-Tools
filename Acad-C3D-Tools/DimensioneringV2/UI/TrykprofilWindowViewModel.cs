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

        public ObservableCollection<PressureProfileEntry>? Profile { get; private set; }
        public PlotModel? PressurePlot { get; private set; }
        public TrykprofilWindowViewModel() { }
        public void LoadData(IEnumerable<PressureProfileEntry> profile)
        {
            Profile = new(profile);

            PressurePlot = new PlotModel { Title = "Pressure profile" };

            PressurePlot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Distance [m]"
            });
            PressurePlot.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Pressure [bar]"
            });

            var supplySeries = new LineSeries 
            {
                Title = "Supply",
                Color = OxyColor.Parse("#FFFF0000")
            };
            var returnSeries = new LineSeries 
            { 
                Title = "Return",
                Color = OxyColor.Parse("#FF0000FF")
            };

            foreach (var p in Profile)
            {
                supplySeries.Points.Add(new DataPoint(p.Distance, p.SupplyPressure));
                returnSeries.Points.Add(new DataPoint(p.Distance, p.ReturnPressure));
            }

            PressurePlot.Series.Add(supplySeries);
            PressurePlot.Series.Add(returnSeries);
            PressurePlot.InvalidatePlot(true);
        }
    }
}
