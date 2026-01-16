using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.UI;

using Mapsui;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

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
            var settings = HydraulicSettingsService.Instance.Settings;
            var slConfig = settings.PipeConfigSL;
            var flConfig = settings.PipeConfigFL;

            var afs = features.Cast<AnalysisFeature>();

            // Calculate data for service lines (stik)
            // Sort by configuration priority, then by DN descending
            var stikTable = afs
                .Where(x => !x.Dim.Equals(default) && x.NumberOfBuildingsConnected == 1)
                .GroupBy(x => x.Dim)
                .OrderBy(g => GetPriorityForPipeType(g.Key.PipeType, slConfig))
                .ThenBy(g => g.Key.NominalDiameter)
                .Select(g => new
                {
                    DimName = g.Key.DimName,
                    TotalLength = g.Sum(x => x.Length),
                    Price = g.Sum(x => x.Length * x.Dim.Price_m),
                    ServiceCount = g.Count(),
                    ServicePrice = g.Count() * g.First().Dim.Price_stk(SegmentType.Stikledning)
                })
                .ToList();

            var stikTotal = new
            {
                TotalPrice = stikTable.Sum(row => row.Price),
                TotalServicePrice = stikTable.Sum(row => row.ServicePrice)
            };

            // Calculate data for supply lines (fls)
            // Sort by configuration priority, then by DN descending
            var flsTable = afs
                .Where(x => !x.Dim.Equals(default) && x.NumberOfBuildingsConnected == 0)
                .GroupBy(x => x.Dim)
                .OrderBy(g => GetPriorityForPipeType(g.Key.PipeType, flConfig))
                .ThenBy(g => g.Key.NominalDiameter)
                .Select(g => new
                {
                    DimName = g.Key.DimName,
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
                window = new PriceSummaryWindow(stikTable, flsTable, grandTotal, stikTotal.TotalPrice + stikTotal.TotalServicePrice, flsTotal.TotalPrice);
                window.Show();
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg(ex);
            }
        }

        /// <summary>
        /// Gets the configuration priority for a pipe type.
        /// Returns int.MaxValue for unconfigured types (they go last).
        /// </summary>
        private int GetPriorityForPipeType(PipeType pipeType, PipeTypeConfiguration config)
        {
            var priority = config.Priorities.FirstOrDefault(p => p.PipeType == pipeType);
            return priority?.Priority ?? int.MaxValue;
        }
    }
}
