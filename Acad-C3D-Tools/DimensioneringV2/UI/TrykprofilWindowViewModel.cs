using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.Models.Trykprofil;

using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace DimensioneringV2.UI
{
    internal class TrykprofilWindowViewModel : ObservableObject
    {
        internal Dispatcher? Dispatcher { get; set; }
        public PlotModel? PressurePlot { get; private set; }
        public PlotModel? PressurePlot2 { get; private set; }
        public TrykprofilWindowViewModel() { }
        public void LoadData(IEnumerable<PressureProfileEntry> profile, PressureData pdata)
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
                Color = OxyColor.Parse("#FF00FF00")
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

            PressurePlot2 = new PlotModel { Title = "Trykprofil" };

            PressurePlot2.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Distance [m]"
            });
            PressurePlot2.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Tryk [bar]",
                Minimum = 1,
                AbsoluteMinimum = 0,
                Maximum = Math.Ceiling(profile.First().SPbar)
            });
            var supplySeries2 = new LineSeries
            {
                Title = "Frem",
                Color = OxyColor.Parse("#FFFF0000")
            };
            var returnSeries2 = new LineSeries
            {
                Title = "Retur",
                Color = OxyColor.Parse("#FF0000FF")
            };

            //Add data
            foreach (var p in profile)
            {
                elevationSeries.Points.Add(new DataPoint(p.Length, p.Elevation));
                supplySeries.Points.Add(new DataPoint(p.Length, p.SPmVS));
                returnSeries.Points.Add(new DataPoint(p.Length, p.RPmVS));
                supplySeries2.Points.Add(new DataPoint(p.Length, p.SPbar));
                returnSeries2.Points.Add(new DataPoint(p.Length, p.RPbar));
            }

            PressurePlot.Series.Add(elevationSeries);
            PressurePlot.Series.Add(supplySeries);
            PressurePlot.Series.Add(returnSeries);

            PressurePlot2.Series.Add(supplySeries2);
            PressurePlot2.Series.Add(returnSeries2);

            //Annotate plots
            var maxKote = pdata.MaxElevation;
            var maxKoteTxt = maxKote.ToString("0.##") + " mVS";

            var fp = profile.First();
            var startKote = fp.Elevation;
            var startKoteTxt = startKote.ToString("0.##") + " m";

            var minHoldeTryk = pdata.MaxElevation;

            var supplyPointLine = new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = startKote,
                Color = OxyColor.FromRgb(40, 40, 100),
                LineStyle = LineStyle.Dash,
                StrokeThickness = 1
            };
            PressurePlot.Annotations.Add(supplyPointLine);

            var supplyPointTxt = new TextAnnotation
            {
                Text = $"Forsyningskote: {startKoteTxt}  ",
                TextHorizontalAlignment = HorizontalAlignment.Right,
                TextVerticalAlignment = VerticalAlignment.Top,
                TextPosition = new DataPoint(profile.Last().Length, startKote),
                Stroke = OxyColors.Transparent,
                Background = OxyColors.Transparent
            };
            PressurePlot.Annotations.Add(supplyPointTxt);

            if (minHoldeTryk > 0 &&
                Services.GraphSettingsService.Instance.Settings.ShowMinimumHoldetrykLineAndLabel)
            {
                var minHoldeTrykLine = new LineAnnotation
                {
                    Type = LineAnnotationType.Horizontal,
                    Y = maxKote,
                    Color = OxyColor.FromRgb(40, 40, 100),
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 1
                };
                PressurePlot.Annotations.Add(minHoldeTrykLine);

                var minHoldeTrykString = minHoldeTryk.ToString("0.##") + " mVS";
                var minHoldeTrykTxt = new TextAnnotation
                {
                    Text = $"Min. holdetryk: {minHoldeTrykString}  ",
                    TextHorizontalAlignment = HorizontalAlignment.Right,
                    TextVerticalAlignment = VerticalAlignment.Bottom,
                    TextPosition = new DataPoint(profile.Last().Length, maxKote),
                    Stroke = OxyColors.Transparent,
                    Background = OxyColors.Transparent
                };
                PressurePlot.Annotations.Add(minHoldeTrykTxt);
            }            

            var tillægTilHoldetryk = pdata.TillægTilHoldetryk;
            if (tillægTilHoldetryk != 0 &&
                Services.GraphSettingsService.Instance.Settings.ShowTillægTilHoldetrykLineAndLabel)
            {
                var tillægString = tillægTilHoldetryk
                    .ToString("0.##") + " mVS";

                var tillægLine = new LineAnnotation
                {
                    Type = LineAnnotationType.Horizontal,
                    Y = pdata.MaxElevation + tillægTilHoldetryk,
                    Color = OxyColor.FromRgb(40, 40, 100),
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 1
                };
                PressurePlot.Annotations.Add(tillægLine);

                var tillægTxt = new TextAnnotation
                {
                    Text = $"Tillæg til holdetryk: {tillægString}  ",
                    TextHorizontalAlignment = HorizontalAlignment.Right,
                    TextVerticalAlignment = VerticalAlignment.Bottom,
                    TextPosition = new DataPoint(profile.Last().Length,
                    pdata.MaxElevation + tillægTilHoldetryk),
                    Stroke = OxyColors.Transparent,
                    Background = OxyColors.Transparent
                };
                PressurePlot.Annotations.Add(tillægTxt);
            }

            //Supply dp
            fp = profile.First();
            var supplyDp = fp.SPmVS - fp.RPmVS;
            var supplyDpString = supplyDp.ToString("0.##") + " mVS";

            var supplyDpLine = new LineSeries
            {
                Color = OxyColor.FromRgb(40, 40, 100),
                LineStyle = LineStyle.Dash,
                StrokeThickness = 1,
            };
            supplyDpLine.Points.Add(new DataPoint(0, fp.SPmVS));
            supplyDpLine.Points.Add(new DataPoint(0, fp.RPmVS));
            PressurePlot.Series.Add(supplyDpLine);

            var supplyDpTxt = new TextAnnotation
            {
                Text = $"  Dp: {supplyDpString}",
                TextHorizontalAlignment = HorizontalAlignment.Left,
                TextVerticalAlignment = VerticalAlignment.Middle,
                TextPosition = new DataPoint(
                    0, fp.RPmVS + (fp.SPmVS - fp.RPmVS) / 2),
                Stroke = OxyColors.Transparent,
                Background = OxyColors.Transparent
            };
            PressurePlot.Annotations.Add(supplyDpTxt);

            //Client dp
            var lp = profile.Last();
            var clientDp = lp.SPmVS - lp.RPmVS;
            var clientDpString = clientDp.ToString("0.##") + " mVS";

            var clientDpLine = new LineSeries
            {
                Color = OxyColor.FromRgb(40, 40, 100),
                LineStyle = LineStyle.Dash,
                StrokeThickness = 1,
            };
            clientDpLine.Points.Add(new DataPoint(lp.Length, lp.SPmVS));
            clientDpLine.Points.Add(new DataPoint(lp.Length, lp.RPmVS));
            PressurePlot.Series.Add(clientDpLine);

            var clientDpTxt = new TextAnnotation
            {
                Text = $"Dp: {clientDpString}  ",
                TextHorizontalAlignment = HorizontalAlignment.Right,
                TextVerticalAlignment = VerticalAlignment.Middle,
                TextPosition = new DataPoint(
                    lp.Length, lp.RPmVS + (lp.SPmVS - lp.RPmVS) / 2),
                Stroke = OxyColors.Transparent,
                Background = OxyColors.Transparent
            };
            PressurePlot.Annotations.Add(clientDpTxt);

            //Redraw plot
            PressurePlot.InvalidatePlot(true);
            PressurePlot2.InvalidatePlot(true);
        }
    }
}
