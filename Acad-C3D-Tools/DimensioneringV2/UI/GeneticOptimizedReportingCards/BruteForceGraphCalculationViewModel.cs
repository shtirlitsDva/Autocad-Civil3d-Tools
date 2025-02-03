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
        private long spanningTreesCount;

        [ObservableProperty]
        private long calculatedTrees;

        [ObservableProperty]
        private double cost;
    }
}
