using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.UI
{
    public partial class BruteForceGraphCalculationViewModel : GraphCalculationBaseViewModel
    {
        [ObservableProperty]
        private string? nonBridgesCount;

        [ObservableProperty]
        private int steinerTreesFound; 

        [ObservableProperty]
        private int calculatedTrees;

        [ObservableProperty]
        private double cost;
    }
}
