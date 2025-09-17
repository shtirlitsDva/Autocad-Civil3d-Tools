using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI;

using Mapsui;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    internal class CalculatePrice
    {
        internal void Execute(IEnumerable<IFeature> features)
        {
            var afs = features.Cast<AnalysisFeature>();
            // Calculate data for service lines (stik)
            var stikTable = afs
                .Where(x => !x.Dim.Equals(default) && x.NumberOfBuildingsConnected == 1)
                .GroupBy(x => x.Dim.DimName)
                .Select(g => new
                {
                    DimName = g.Key,
                    TotalLength = g.Sum(x => x.Length),
                    Price = g.Sum(x => x.Length * x.Dim.Price_m),
                    ServiceCount = g.Count(),
                    ServicePrice = g.Count() * g.First().Dim.Price_stk(NorsynHydraulicCalc.SegmentType.Stikledning)
                })
                .ToList();

            var stikTotal = new
            {
                TotalPrice = stikTable.Sum(row => row.Price),
                TotalServicePrice = stikTable.Sum(row => row.ServicePrice)
            };

            // Calculate data for supply lines (fls)
            var flsTable = afs
                .Where(x => !x.Dim.Equals(default) && x.NumberOfBuildingsConnected == 0)
                .GroupBy(x => x.Dim.DimName)
                .Select(g => new
                {
                    DimName = g.Key,
                    TotalLength = g.Sum(x => x.Length),
                    Price = g.Sum(x => x.Length * x.Dim.Price_m)
                })
                .ToList();

            var flsTotal = new
            {
                TotalPrice = flsTable.Sum(row => row.Price)
            };

            var grandTotal = stikTotal.TotalPrice + stikTotal.TotalServicePrice + flsTotal.TotalPrice;

            PriceSummaryWindow window;
            try
            {
                window = new PriceSummaryWindow(stikTable, flsTable, grandTotal);
                window.Show();
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg(ex);
            }
        }
    }
}
