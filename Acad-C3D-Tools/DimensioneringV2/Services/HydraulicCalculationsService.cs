using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class HydraulicCalculationsService
    {
        private static DataService _dataService = DataService.Instance;
        internal static void PerformCalculations()
        {
            var graphs = GraphCreationService.CreateGraphsFromFeatures(_dataService.Features);

            foreach (var graph in graphs)
            {
                
                    
            }
        }
    }
}
